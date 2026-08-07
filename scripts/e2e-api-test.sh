#!/usr/bin/env bash
# End-to-end smoke test of the Fleet Rental MVP flow.
set -u
export PATH="$PATH:/c/Program Files/nodejs"
API=http://localhost:5180/api
TMP=$(mktemp -d)
pass=0; fail=0
ok(){ echo "  PASS: $1"; pass=$((pass+1)); }
no(){ echo "  FAIL: $1"; fail=$((fail+1)); }
# Evaluate an expression against JSON on stdin, where `d` is the parsed document.
q(){ node -e 'let s="";process.stdin.on("data",c=>s+=c).on("end",()=>{try{const d=JSON.parse(s);const r=eval(process.argv[1]);console.log(r===undefined?"":r)}catch(e){console.log("")}})' "$1"; }

echo "== 1. Car listing (feature 1) =="
CARS=$(curl -s $API/cars)
COUNT=$(echo "$CARS" | q 'd.length')
[ "$COUNT" -ge 5 ] 2>/dev/null && ok "listed $COUNT cars" || no "expected >=5 cars, got '$COUNT'"
CAR_ID=$(echo "$CARS" | q 'd[0].id')
echo "  car: $(echo "$CARS" | q 'd[0].name') | photo: $(echo "$CARS" | q '(d[0].primaryPhotoUrl||"none").slice(0,50)') | availableToday: $(echo "$CARS" | q 'd[0].availableToday')"

echo "== 2. Client signup (feature 5) =="
EMAIL="client$RANDOM$RANDOM@test.com"
SIGNUP=$(curl -s -X POST $API/auth/signup -H "Content-Type: application/json" \
  -d "{\"email\":\"$EMAIL\",\"password\":\"TestPass123\",\"fullName\":\"Test Client\"}")
CTOK=$(echo "$SIGNUP" | q 'd.accessToken')
[ -n "$CTOK" ] && ok "signed up, got token" || no "signup failed: $SIGNUP"
ROLE=$(echo "$SIGNUP" | q 'd.user.role')
[ "$ROLE" = "Client" ] && ok "public signup yields role=Client" || no "role was '$ROLE'"

echo "== 3. Weak password rejected =="
BAD=$(curl -s -o /dev/null -w "%{http_code}" -X POST $API/auth/signup -H "Content-Type: application/json" \
  -d '{"email":"x@y.com","password":"short","fullName":"X"}')
[ "$BAD" = "400" ] && ok "short password -> 400" || no "expected 400, got $BAD"

echo "== 4. Admin login (feature 5) =="
ALOGIN=$(curl -s -X POST $API/auth/admin/login -H "Content-Type: application/json" \
  -d '{"email":"admin@fleetrental.local","password":"ChangeMe!2026"}')
ATOK=$(echo "$ALOGIN" | q 'd.accessToken')
[ -n "$ATOK" ] && ok "admin logged in" || no "admin login failed: $ALOGIN"

echo "== 5. Client blocked from admin login =="
CBLOCK=$(curl -s -o /dev/null -w "%{http_code}" -X POST $API/auth/admin/login -H "Content-Type: application/json" \
  -d "{\"email\":\"$EMAIL\",\"password\":\"TestPass123\"}")
[ "$CBLOCK" = "403" ] && ok "client on admin login -> 403" || no "expected 403, got $CBLOCK"

echo "== 6. Booking request with event details (feature 3) =="
B1=$(curl -s -X POST $API/bookings -H "Content-Type: application/json" -H "Authorization: Bearer $CTOK" \
  -d "{\"carId\":\"$CAR_ID\",\"startDate\":\"2026-10-01\",\"endDate\":\"2026-10-05\",\"eventName\":\"Autumn Product Launch\",\"eventType\":\"ProductLaunch\",\"eventLocation\":\"Dubai World Trade Centre\",\"expectedAttendance\":450,\"clientNotes\":\"Wrapped in brand livery\"}")
B1_ID=$(echo "$B1" | q 'd.id')
[ -n "$B1_ID" ] && ok "booking created" || no "booking failed: $B1"
echo "  status: $(echo "$B1" | q 'd.status') | days: $(echo "$B1" | q 'd.totalDays') | event: $(echo "$B1" | q 'd.event.name+" / "+d.event.type')"

echo "== 7. Admin sees it in the queue (feature 4) =="
QN=$(curl -s "$API/bookings?status=Pending" -H "Authorization: Bearer $ATOK" | q 'd.length')
[ "$QN" -ge 1 ] 2>/dev/null && ok "queue has $QN pending" || no "queue empty"

echo "== 8. Client cannot read the admin queue =="
FORB=$(curl -s -o /dev/null -w "%{http_code}" "$API/bookings" -H "Authorization: Bearer $CTOK")
[ "$FORB" = "403" ] && ok "client on admin queue -> 403" || no "expected 403, got $FORB"

echo "== 9. Second client books OVERLAPPING dates =="
EMAIL2="client$RANDOM$RANDOM@test.com"
CTOK2=$(curl -s -X POST $API/auth/signup -H "Content-Type: application/json" \
  -d "{\"email\":\"$EMAIL2\",\"password\":\"TestPass123\",\"fullName\":\"Rival Client\"}" | q 'd.accessToken')
B2=$(curl -s -X POST $API/bookings -H "Content-Type: application/json" -H "Authorization: Bearer $CTOK2" \
  -d "{\"carId\":\"$CAR_ID\",\"startDate\":\"2026-10-04\",\"endDate\":\"2026-10-08\",\"eventName\":\"Rival Trade Show\",\"eventType\":\"TradeShow\",\"eventLocation\":\"Abu Dhabi\"}")
B2_ID=$(echo "$B2" | q 'd.id')
[ -n "$B2_ID" ] && ok "overlapping request accepted while both pending (by design)" || no "rejected: $B2"

echo "== 10. Approve the first (feature 4) =="
A1=$(curl -s -o /dev/null -w "%{http_code}" -X POST $API/bookings/$B1_ID/approve \
  -H "Content-Type: application/json" -H "Authorization: Bearer $ATOK" -d '{"reason":"Confirmed"}')
[ "$A1" = "200" ] && ok "first booking approved -> 200" || no "approve failed -> $A1"

echo "== 11. DOUBLE-BOOKING: approve the overlapping one =="
A2C=$(curl -s -o "$TMP/a2.json" -w "%{http_code}" -X POST $API/bookings/$B2_ID/approve \
  -H "Content-Type: application/json" -H "Authorization: Bearer $ATOK" -d '{}')
if [ "$A2C" = "409" ]; then
  ok "overlapping approval BLOCKED -> 409"
  echo "  message: $(q 'd.detail' < "$TMP/a2.json")"
else
  no "DOUBLE-BOOKING GOT THROUGH -> $A2C"
fi

echo "== 12. Calendar reflects the approval (feature 2) =="
AV=$(curl -s "$API/cars/$CAR_ID/availability?from=2026-09-25&to=2026-10-15")
echo "  booked : $(echo "$AV" | q 'JSON.stringify(d.bookedDates)')"
echo "  pending: $(echo "$AV" | q 'JSON.stringify(d.pendingDates)')"
BD=$(echo "$AV" | q 'd.bookedDates.length')
[ "$BD" = "5" ] && ok "5 days blocked (Oct 1-5 inclusive)" || no "expected 5 booked days, got $BD"

echo "== 13. Fleet-wide calendar (feature 4) =="
FN=$(curl -s "$API/fleet/availability?from=2026-09-25&to=2026-10-15" -H "Authorization: Bearer $ATOK" | q 'd.cars.length')
[ "$FN" -ge 5 ] 2>/dev/null && ok "fleet calendar covers $FN cars" || no "fleet calendar got $FN cars"

echo "== 14. Reject flow (feature 6 notification fires) =="
R=$(curl -s -o /dev/null -w "%{http_code}" -X POST $API/bookings/$B2_ID/reject \
  -H "Content-Type: application/json" -H "Authorization: Bearer $ATOK" -d '{"reason":"Vehicle already committed"}')
[ "$R" = "200" ] && ok "rejected -> 200" || no "reject failed -> $R"

echo "== 15. Client sees own bookings only =="
MN=$(curl -s "$API/bookings/mine" -H "Authorization: Bearer $CTOK" | q 'd.length')
[ "$MN" = "1" ] && ok "client sees exactly their 1 booking" || no "expected 1, got $MN"

echo "== 16. Cross-client read blocked =="
X=$(curl -s -o /dev/null -w "%{http_code}" "$API/bookings/$B1_ID" -H "Authorization: Bearer $CTOK2")
[ "$X" = "403" ] && ok "reading another client's booking -> 403" || no "expected 403, got $X"

echo "== 17. Anonymous write blocked =="
AN=$(curl -s -o /dev/null -w "%{http_code}" -X POST $API/bookings -H "Content-Type: application/json" \
  -d "{\"carId\":\"$CAR_ID\",\"startDate\":\"2026-12-01\",\"endDate\":\"2026-12-02\",\"eventName\":\"x\",\"eventLocation\":\"y\"}")
[ "$AN" = "401" ] && ok "anonymous booking -> 401" || no "expected 401, got $AN"

echo "== 18. Past dates rejected =="
PD=$(curl -s -o /dev/null -w "%{http_code}" -X POST $API/bookings -H "Content-Type: application/json" \
  -H "Authorization: Bearer $CTOK" \
  -d "{\"carId\":\"$CAR_ID\",\"startDate\":\"2020-01-01\",\"endDate\":\"2020-01-02\",\"eventName\":\"x\",\"eventLocation\":\"y\"}")
[ "$PD" = "400" ] && ok "past start date -> 400" || no "expected 400, got $PD"

rm -rf "$TMP"
echo
echo "================================"
echo "  PASSED: $pass   FAILED: $fail"
echo "================================"
[ "$fail" -eq 0 ] || exit 1
