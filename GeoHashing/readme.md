### Why Latitude/Longitude Search Doesn’t Scale — and How Geohash Solves It

Finding “all places within 1 km of me” sounds simple.

You have latitude and longitude stored in your database.
Run a query. Return all rows within the desired radius.

But once your app grows from a few hundred rows to millions,
the naïve approach becomes painfully slow.

This blog explains why pure lat/lng search does not scale,
why the problem is not specific to geography at all,
and how geohash turns nearby search into a fast, index-friendly operation.

We’ll go step by step with small paper examples — no heavy math.

📍 1. The Real Problem: Nearby Search Is a 2D Query

Your database stores location like:

lat FLOAT
lng FLOAT


But SQL indexes (B-trees) are 1D structures.

Nearby search is a 2D problem.

A typical “within X meters” search is like this:

Circle around a point (lat0, lng0) with radius R


SQL doesn’t natively understand circles or 2D proximity.

That’s the root of everything that follows.

🧩 2. Why Latitude or Longitude Alone Cannot Filter Well
❌ Searching by latitude alone gives you a horizontal band across the whole globe.
lat between 12 and 13


This includes:

India

Africa

USA

China

All because they share similar latitude but are thousands of km apart.

❌ Searching by longitude alone gives you a vertical band across the globe.

So using only lat or only lng barely reduces your search space.

🧩 3. Why a Multi-Column Index on (lat, lng) Still Struggles

You might think:

“Let me index both columns: INDEX(lat, lng)
Now it should be fast.”

But B-tree indexes store the rows in lexicographic order:

(lat1, lng1)
(lat1, lng2)
(lat1, lng3)
...
(lat2, lng1)
(lat2, lng2)
...


This helps only for these patterns:

✔ WHERE lat = X AND lng BETWEEN Y1 AND Y2
✔ WHERE lat = X
✔ WHERE lat BETWEEN X1 AND X2 (partial)

But not for 2D ranges like:

lat BETWEEN x1 AND x2
AND
lng BETWEEN y1 AND y2


This query represents a rectangle on the map —
but in lexicographic index order, it becomes scattered chunks.

Example:

(10, 1)   ❌
(10, 5)   ✔
(10, 9)   ✔
(10, 20)  ❌
(11, 3)   ❌
(11, 10)  ✔
(11, 15)  ✔
(12, 5)   ✔
(12, 30)  ❌


SQL must scan many irrelevant rows.
The index cannot “jump directly” to the 2D region.

This is not a lat/lng issue —
it’s a general problem with applying range filters on two independent columns.

🌍 4. Why Direct Distance Search Is Slow

Distance is measured in meters:

1° of latitude ≈ 111 km

1° of longitude varies with latitude

distance formula (Haversine) is expensive to run on millions of rows

So you can’t run:

WHERE distance(lat, lng, user_lat, user_lng) <= 1 km


This forces a full table scan.

⚠️ 5. The Band-Aid Solution: Bounding Box + Distance Filter

Every scalable lat/lng search uses two steps:

Step 1 — Compute a bounding rectangle

Convert radius (meters) to degrees:

lat_min, lat_max
lng_min, lng_max


Run:

WHERE lat BETWEEN lat_min AND lat_max
  AND lng BETWEEN lng_min AND lng_max


This still returns many false positives, but fewer than scanning everything.

Step 2 — Apply exact Haversine distance

Filter only the remaining rows.

This works, but not the fastest.

🚀 6. Enter Geohash — A Smarter Way to Index 2D Space

Geohash converts (lat, lng) into a simple string:

gcpuvx
ezs42
u10j0


Properties:

Nearby points share common prefixes

Longer prefix = smaller area

Geohash turns 2D → 1D in a way that preserves locality

You can index it using a simple SQL B-tree index

This means you can search a region using:

WHERE geohash LIKE 'gcpuv%'


This is extremely fast because prefix searches on strings are index-friendly.

🧩 7. Tiny Paper Example of a Geohash Grid

Imagine a 4×4 grid:

A0 A1 A2 A3
B0 B1 B2 B3
C0 C1 C2 C3
D0 D1 D2 D3


Each cell = 1 geohash.

User in cell C3.

Nearby cells:

C2 C3
D2 D3


SQL query:

geohash IN ('C3%', 'C2%', 'D2%', 'D3%')


We read only 4 groups instead of scanning world.

This is why geohash is fast.

🔍 8. Does Geohash Give Exact Searching?

It gives near-direct searching.

You need one tiny extra step:

Get the user’s geohash (precision depends on radius)

Query that geohash + its 8 neighbors

Do exact distance check on only 20–300 rows

This is insanely faster than scanning millions.

So geohash:

replaces bounding box math

avoids scanning huge ranges

groups nearby points tightly

enables extremely fast prefix searches


The real problem isn’t lat/lng — it’s SQL’s inability to index 2D ranges efficiently.

Latitude and longitude form a 2D search, but SQL’s indexes are 1D.

That’s why multi-column ranges (BETWEEN on two columns) perform poorly.

Geohash solves this by converting 2D space into a 1D prefix that preserves proximity.

Searching nearby places becomes as simple as a prefix match on a string.

📝 Final Thoughts

If your app supports:

restaurants nearby

delivery partners nearby

users within a radius

geo alerts

geo clustering

maps with pins