# Pagination Strategies: Offset vs. Cursor

When designing APIs for large datasets, choosing the right pagination strategy is critical for performance and user experience. Below is a comparison of the two primary methods.

## 1. Offset-Based Pagination (The "Page Number" Method)

This is the traditional method used by most websites. The client specifically asks for a "page" of data.

### How it works
The client sends two parameters: `PageNumber` (or Offset) and `PageSize` (Limit).

**SQL Logic:**
```sql
OFFSET (PageNumber - 1) * PageSize ROWS 
FETCH NEXT PageSize ROWS ONLY
```
### The Scenario

Imagine you have a database of 100,000 users, ordered by SignUpDate. You want to get Page 3 with a size of 10.

The database sorts all 100,000 users by SignUpDate.

It counts through the first 20 records (Page 1 + Page 2) and discards them.

It picks the next 10 records (Page 3) and returns them.

Pros & Cons
✅ Easy to implement: Very standard in SQL and LINQ (e.g., .Skip(20).Take(10)).

✅ Random Access: You can jump straight to Page 50 without seeing Pages 1-49.

❌ Performance Hit at Scale: To get Page 10,000, the database must load and "skip" the first 99,990 rows. This becomes extremely slow with large datasets.

❌ Data Drift: If a new user signs up while you are looking at Page 1, the entire list shifts down. When you click "Next Page," you might see the same person again (duplicate) or miss someone entirely.

### 2. Cursor-Based Pagination (The "Infinite Scroll" Method)
This is the method used by social media feeds (Twitter/X, Facebook) and the Slack API. Instead of saying "give me page 5," the client says "give me 10 items after this specific item."

How it works
The "Cursor" is a unique pointer (usually the ID or a Timestamp of the last item received).

```
WHERE Id > @LastSeenId 
ORDER BY Id ASC 
LIMIT 10
```

The Scenario
You have the same 100,000 users. You just requested 10 users, and the last user in that list had Id = 55.

The client requests: ?limit=10&cursor=55

The database goes directly to Id = 55 using an index (very fast).

It grabs the next 10 records immediately.

Pros & Cons
✅ Extremely Fast: It doesn't need to count or skip previous rows. It jumps straight to the data using the index. Performance is constant whether you are on the 1st request or the 1,000th.

✅ Stable Data: If new users are added to the top of the list, your cursor stays pointing at the specific ID you left off at. You won't see duplicates or miss items.

❌ No Random Access: You cannot jump to "Page 50." You must scroll through the data sequentially.

❌ Complex Implementation: You need to manage encoding cursors (often Base64 strings) and handle sorting by non-unique columns (like CreatedDate) carefully.