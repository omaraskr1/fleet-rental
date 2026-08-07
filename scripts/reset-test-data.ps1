# Clears transactional data so the e2e suite starts from a known state.
# Leaves seeded cars and the admin account in place.
$cs = "Server=.\SQLEXPRESS;Database=FleetRental;Trusted_Connection=True;TrustServerCertificate=True"
Add-Type -AssemblyName System.Data
$conn = New-Object System.Data.SqlClient.SqlConnection $cs
$conn.Open()
$cmd = $conn.CreateCommand()
$cmd.CommandText = @"
DELETE FROM BookedDays;
DELETE FROM Bookings;
DELETE FROM Events;
DELETE FROM DeviceTokens;
DELETE FROM Users WHERE Role = 'Client';
"@
$cmd.ExecuteNonQuery() | Out-Null
$cmd.CommandText = "SELECT (SELECT COUNT(*) FROM Bookings) AS b, (SELECT COUNT(*) FROM BookedDays) AS d, (SELECT COUNT(*) FROM Cars) AS c, (SELECT COUNT(*) FROM Users) AS u;"
$r = $cmd.ExecuteReader()
while ($r.Read()) { "reset -> bookings=$($r['b']) bookedDays=$($r['d']) cars=$($r['c']) users=$($r['u'])" }
$r.Close(); $conn.Close()
