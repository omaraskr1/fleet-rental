#!/usr/bin/env bash
# Seeds seven months of Approved bookings (Jan-Jul, ending the month before the
# current one) purely so the Revenue Forecast module (Admin > Analytics) and
# Category Demand module have enough history to render for a client demo.
#
# Why this can't go through the normal booking API: Booking.Request() rejects
# any start date in the past, deliberately, "for any caller including seeding
# and admin tools" (see backend/src/FleetRental.Domain/Entities/Booking.cs).
# That rule is correct for real usage and this script does not touch it —
# instead it writes directly to the demo database's Events/Bookings/BookedDays
# tables, bypassing the API entirely. Do not point this at a real customer's
# database; it is a demo-data tool for the Docker/dev stack only.
#
# Idempotent: re-running it is a no-op once the marker event exists.
set -euo pipefail

API=${API:-http://localhost:5180/api}
TENANT=${TENANT_CODE:-demo-fleet}
DB_CONTAINER=${DB_CONTAINER:-eventdrive-db-1}
DB_NAME=${DB_NAME:-FleetRental}
SA_PASSWORD=${SA_PASSWORD:-Test-Only-Pwd-2026!}
DEMO_CLIENT_EMAIL=${DEMO_CLIENT_EMAIL:-revenue-demo@demo-fleet.local}
DEMO_CLIENT_PASSWORD=${DEMO_CLIENT_PASSWORD:-RevenueDemo123}
MARKER="Demo history seed"

sql() {
  MSYS_NO_PATHCONV=1 docker exec -i "$DB_CONTAINER" /opt/mssql-tools18/bin/sqlcmd \
    -S localhost -U sa -P "$SA_PASSWORD" -C -d "$DB_NAME" -h -1 -W "$@"
}

echo "== Ensuring the demo client account exists =="
curl -s -o /dev/null -X POST "$API/auth/signup" -H "Content-Type: application/json" -H "X-Tenant-Code: $TENANT" \
  -d "{\"email\":\"$DEMO_CLIENT_EMAIL\",\"password\":\"$DEMO_CLIENT_PASSWORD\",\"fullName\":\"Revenue Demo Client\"}" || true

CLIENT_ID=$(sql -Q "SET NOCOUNT ON; SELECT CAST(Id AS varchar(36)) FROM Users WHERE Email='$DEMO_CLIENT_EMAIL'" | tr -d '[:space:]')
if [ -z "$CLIENT_ID" ]; then
  echo "FAILED: could not find or create $DEMO_CLIENT_EMAIL — is the API up at $API?"
  exit 1
fi
echo "  client id: $CLIENT_ID"

TENANT_ID=$(sql -Q "SET NOCOUNT ON; SELECT CAST(Id AS varchar(36)) FROM Tenants WHERE Code='$TENANT'" | tr -d '[:space:]')
ADMIN_ID=$(sql -Q "SET NOCOUNT ON; SELECT TOP 1 CAST(Id AS varchar(36)) FROM Users WHERE TenantId='$TENANT_ID' AND Role='Admin'" | tr -d '[:space:]')

echo "== Checking whether the seed already ran =="
ALREADY=$(sql -Q "SET NOCOUNT ON; SELECT COUNT(*) FROM Events WHERE TenantId='$TENANT_ID' AND Notes='$MARKER'" | tr -d '[:space:]')
if [ "$ALREADY" != "0" ]; then
  echo "Already seeded ($ALREADY events found) — nothing to do. Delete those Events (cascades to Bookings/BookedDays) to reseed."
  exit 0
fi

echo "== Inserting seven months of approved bookings (Jan-Jul) =="
sql -Q "
DECLARE @Tenant uniqueidentifier = '$TENANT_ID';
DECLARE @Client uniqueidentifier = '$CLIENT_ID';
DECLARE @Admin uniqueidentifier = '$ADMIN_ID';
DECLARE @Marker nvarchar(50) = N'$MARKER';

DECLARE @Mustang uniqueidentifier = (SELECT Id FROM Cars WHERE TenantId=@Tenant AND Name='Mustang Convertible');
DECLARE @Transit uniqueidentifier = (SELECT Id FROM Cars WHERE TenantId=@Tenant AND Name='Ford Transit Roadshow');
DECLARE @BMW     uniqueidentifier = (SELECT Id FROM Cars WHERE TenantId=@Tenant AND Name='BMW 5 Series');
DECLARE @Merc    uniqueidentifier = (SELECT Id FROM Cars WHERE TenantId=@Tenant AND Name='Mercedes V-Class');
DECLARE @RRover  uniqueidentifier = (SELECT Id FROM Cars WHERE TenantId=@Tenant AND Name='Range Rover Sport');

IF @Mustang IS NULL OR @Transit IS NULL OR @BMW IS NULL OR @Merc IS NULL OR @RRover IS NULL
BEGIN
  RAISERROR('One or more expected demo cars are missing for this tenant — has the seeded fleet been renamed?', 16, 1);
  RETURN;
END

DECLARE @Demo TABLE (
  CarId uniqueidentifier, EventName nvarchar(200), EventType nvarchar(30), Location nvarchar(200),
  Attendance int, StartDate date, EndDate date
);

INSERT INTO @Demo VALUES
-- January
(@BMW,     N'Riverside Product Reveal',      'ProductLaunch',   N'Dubai Marina',            180, '2026-01-05', '2026-01-08'),
(@Merc,    N'Gulf Business Summit',          'Conference',      N'Dubai World Trade Centre',320, '2026-01-15', '2026-01-18'),
(@Mustang, N'Coastline Fashion Shoot',       'Photoshoot',      N'Jumeirah Beach',           40, '2026-01-22', '2026-01-24'),
-- February
(@Transit, N'Tech Roadshow Leg 1',           'RoadShow',        N'Abu Dhabi',               250, '2026-02-03', '2026-02-06'),
(@BMW,     N'Private Wedding Transfer',      'Wedding',         N'Palm Jumeirah',            60, '2026-02-10', '2026-02-13'),
(@Merc,    N'Regional Sales Kickoff',        'CorporateEvent',  N'Sharjah',                 150, '2026-02-18', '2026-02-20'),
(@RRover,  N'Luxury Brand Activation',       'ProductLaunch',   N'Downtown Dubai',          200, '2026-02-24', '2026-02-26'),
-- March
(@Mustang, N'Motor Show Hero Car',           'Other',           N'Dubai World Trade Centre',500, '2026-03-02', '2026-03-05'),
(@Transit, N'University Career Fair Tour',   'RoadShow',        N'Sharjah',                 300, '2026-03-09', '2026-03-12'),
(@BMW,     N'Executive Press Day',           'CorporateEvent',  N'Dubai Media City',        80,  '2026-03-16', '2026-03-19'),
(@Merc,    N'Charity Gala Transfers',        'CorporateEvent',  N'Emirates Palace',         220, '2026-03-23', '2026-03-26'),
-- April
(@RRover,  N'Outdoor Adventure Launch',      'ProductLaunch',   N'Al Ain',                  140, '2026-04-01', '2026-04-04'),
(@Mustang, N'Fashion Week Photoshoot',       'Photoshoot',      N'DIFC',                     50, '2026-04-08', '2026-04-11'),
(@Transit, N'Retail Roadshow Leg 2',         'RoadShow',        N'Ras Al Khaimah',          280, '2026-04-15', '2026-04-19'),
(@BMW,     N'VIP Delegation Transfer',       'CorporateEvent',  N'Dubai',                    30, '2026-04-22', '2026-04-24'),
-- May
(@Merc,    N'Wedding Convoy',                'Wedding',         N'Abu Dhabi',                70, '2026-05-06', '2026-05-08'),
(@BMW,     N'Investor Roadshow Stop',        'CorporateEvent',  N'DIFC',                    120, '2026-05-14', '2026-05-16'),
(@Mustang, N'Automotive Editorial Shoot',    'Photoshoot',      N'Al Qudra Desert',          25, '2026-05-21', '2026-05-23'),
(@RRover,  N'Adventure Gear Activation',     'ProductLaunch',   N'Hatta',                   160, '2026-05-27', '2026-05-29'),
-- June
(@Transit, N'Summer Sale Roadshow',          'RoadShow',        N'Dubai',                   260, '2026-06-02', '2026-06-06'),
(@Mustang, N'Luxury Watch Launch',           'ProductLaunch',   N'DIFC',                     90, '2026-06-09', '2026-06-12'),
(@BMW,     N'Diplomatic Transfer Week',      'CorporateEvent',  N'Abu Dhabi',                40, '2026-06-16', '2026-06-19'),
(@Merc,    N'Trade Show Shuttle',            'TradeShow',       N'Dubai World Trade Centre',350, '2026-06-23', '2026-06-27'),
-- July
(@RRover,  N'Summer Campaign Activation',    'ProductLaunch',   N'JBR',                     180, '2026-07-02', '2026-07-06'),
(@Transit, N'National Day Prep Roadshow',    'RoadShow',        N'Sharjah',                 300, '2026-07-09', '2026-07-13'),
(@Mustang, N'Convertible Editorial Shoot',   'Photoshoot',      N'Al Marmoom',               35, '2026-07-16', '2026-07-20'),
(@BMW,     N'Quarterly Board Transfers',     'CorporateEvent',  N'Dubai',                    20, '2026-07-23', '2026-07-26'),
(@Merc,    N'Trade Delegation Welcome',      'TradeShow',       N'Expo City Dubai',         240, '2026-07-28', '2026-07-31');

DECLARE @CarId uniqueidentifier, @EventName nvarchar(200), @EventType nvarchar(30), @Location nvarchar(200),
        @Attendance int, @StartDate date, @EndDate date;

DECLARE demo_cursor CURSOR LOCAL FAST_FORWARD FOR
  SELECT CarId, EventName, EventType, Location, Attendance, StartDate, EndDate FROM @Demo;

OPEN demo_cursor;
FETCH NEXT FROM demo_cursor INTO @CarId, @EventName, @EventType, @Location, @Attendance, @StartDate, @EndDate;

WHILE @@FETCH_STATUS = 0
BEGIN
  DECLARE @EventId uniqueidentifier = NEWID();
  DECLARE @BookingId uniqueidentifier = NEWID();
  DECLARE @CreatedAt datetimeoffset = TODATETIMEOFFSET(CAST(DATEADD(day, -5, @StartDate) AS datetime2), 240);
  DECLARE @DecidedAt datetimeoffset = TODATETIMEOFFSET(CAST(DATEADD(day, -3, @StartDate) AS datetime2), 240);

  INSERT INTO Events (Id, OrganizerId, Name, Type, Location, ExpectedAttendance, Notes, CreatedAt, TenantId)
  VALUES (@EventId, @Client, @EventName, @EventType, @Location, @Attendance, @Marker, @CreatedAt, @Tenant);

  INSERT INTO Bookings
    (Id, CarId, ClientId, EventId, Status, ClientNotes, DecidedAt, DecidedByUserId, DecisionReason,
     NotifiedAt, EndDate, StartDate, CreatedAt, TenantId)
  VALUES
    (@BookingId, @CarId, @Client, @EventId, 'Approved', NULL, @DecidedAt, @Admin, 'Confirmed',
     @DecidedAt, @EndDate, @StartDate, @CreatedAt, @Tenant);

  DECLARE @Day date = @StartDate;
  WHILE @Day <= @EndDate
  BEGIN
    INSERT INTO BookedDays (Id, CarId, BookingId, Date, CreatedAt, TenantId)
    VALUES (NEWID(), @CarId, @BookingId, @Day, @DecidedAt, @Tenant);
    SET @Day = DATEADD(day, 1, @Day);
  END

  FETCH NEXT FROM demo_cursor INTO @CarId, @EventName, @EventType, @Location, @Attendance, @StartDate, @EndDate;
END

CLOSE demo_cursor;
DEALLOCATE demo_cursor;
"

COUNT=$(sql -Q "SET NOCOUNT ON; SELECT COUNT(*) FROM Bookings b JOIN Events e ON e.Id = b.EventId WHERE e.Notes='$MARKER'" | tr -d '[:space:]')
echo "Done — inserted $COUNT approved historical bookings (Jan-Jul 2026) for '$TENANT'."
echo "Log in as the admin and open Admin > Analytics > Revenue Forecast to see it."
