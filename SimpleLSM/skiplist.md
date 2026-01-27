
# The Unsung Hero of LevelDB: Why We Skip Instead of Climb

If you have ever peeked under the hood of **LevelDB** (the fast key-value storage library written by Google), you might expect to find complex balanced trees managing the data in memory. Instead, you find something called a **Skip List**.

It sounds casual—like skipping stones—but it is actually a brilliant probabilistic data structure that beats complex trees at their own game, especially when things get busy.

Here is the lowdown on what it is, why LevelDB uses it, and how it stacks up against the classic Binary Search Tree.

---

### What is a Skip List? (The "Express Lane" Analogy)

Imagine a standard **Linked List**. To find the number `50`, you have to start at the head and visit every single node (`1, 2, 3... 49, 50`). It’s slow—.

Now, imagine that same list, but with **fast lanes** built on top of it.

* **Level 0:** Stops at every number.
* **Level 1:** Stops at every 4th number.
* **Level 2:** Stops at every 10th number.

To find `50`, you take the **Express Lane (Level 2)** as far as you can, then drop down to **Level 1** to get closer, and finally hop onto **Level 0** to find the exact address.

That is a Skip List. It uses layers of pointers to allow you to "skip" over large sections of data, making search, insertion, and deletion extremely fast— on average—just like a balanced tree.

---

### The LevelDB Context: The MemTable

In LevelDB, when you write data, it doesn't go straight to the hard disk. It goes into an in-memory buffer called the **MemTable**.

* The MemTable **must stay sorted** at all times.
* When the MemTable gets full, it is flushed to disk as an immutable file.

To keep this MemTable sorted while thousands of writes are pouring in, LevelDB needs a structure that is fast to write to and easy to read.

---

### The Showdown: Skip Lists vs. Balanced Trees (AVL / Red-Black)

In Computer Science 101, we are taught that Red-Black Trees or AVL Trees are the gold standard for sorted data. So, why did the creators of LevelDB (Jeff Dean and Sanjay Ghemawat) choose the Skip List?

#### 1. Concurrency (The Killer Feature)

This is the main reason.

* **Trees:** When you insert data into a Balanced Tree (like a Red-Black tree), you often have to "rebalance" or "rotate" the tree to keep it optimized. A rotation can affect many nodes at once. If you are writing multi-threaded code, you have to lock large parts of the tree to stop other threads from reading corrupted data while you rotate. This kills performance.
* **Skip Lists:** Inserting a node in a Skip List is very local. You only need to update the pointers of the immediate neighbors. This makes it much easier to support **lock-free reads**. In LevelDB, a writer can insert data while multiple readers are scanning the list without blocking each other.

#### 2. Simplicity of Code

* **Trees:** Implementing a Red-Black tree is notoriously difficult. There are many edge cases for rebalancing (Left-Rotate, Right-Rotate, color flipping). More code = more bugs.
* **Skip Lists:** The algorithm is straightforward. It is essentially a linked list with a randomized height generator. It requires significantly fewer lines of code to implement correctly.

#### 3. Memory Efficiency

* **Trees:** Every node needs pointers to children (Left/Right) plus metadata for balancing (Color or Height integers).
* **Skip Lists:** The "height" of a node is probabilistic. Most nodes are short (Level 0), and very few are tall. On average, a Skip List can use less memory overhead per pointer than a tree, depending on the configuration.

---

### Summary Table

| Feature | Balanced Tree (Red-Black/AVL) | Skip List (LevelDB) |
| --- | --- | --- |
| **Search Speed** |  (Worst case) |  (Average/Probabilistic) |
| **Insertion Speed** |  |  |
| **Concurrent Access** | **Difficult.** Rebalancing requires complex locking. | **Excellent.** Local updates allow simpler locking or lock-free reads. |
| **Implementation** | Complex. Hard to debug. | Simple. Easier to maintain. |
| **Space Usage** | Fixed overhead per node. | Variable. Can be tuned to be very compact. |

### The Verdict

LevelDB uses a Skip List because it provides **tree-like speed () without the tree-like complexity.**

In a high-performance database where you need to dump data into memory as fast as possible while letting other threads read that data simultaneously, the Skip List is the superior tool for the job.

### Example

Here is a step-by-step walkthrough of how a Skip List works using numbers from **1 to 100**.

To make this easy to visualize, let's imagine a Skip List that has already been built with some random "express lanes."

### The Setup: The "Lanes"

Imagine our nodes (1 to 100) are organized into layers (levels).

* **Level 0 (The Slow Lane):** Contains **ALL** numbers: 1, 2, 3, ... 99, 100.
* **Level 1 (The Fast Lane):** Contains randomly promoted numbers (roughly every 2nd or 3rd number).
* **Level 2 (The Express Lane):** Contains even fewer numbers (the "VIPs").

Here is a simplified visual of what a section of that list might look like:

```text
Level 2:  [Start] --------------------------------> [50] -------------------------------------> [End]
Level 1:  [Start] ---------> [25] ----------------> [50] ---------> [75] ---------------------> [End]
Level 0:  [Start] -> [10] -> [25] -> [30] -> [40] -> [50] -> [60] -> [75] -> [80] -> [90] -> [99] -> [End]

```

*(Note: Level 0 actually has every single number 1-100, but I've hidden some for clarity.)*

---

### Scenario 1: Reading Data (Search for `78`)

You want to find the value **78**.
In a normal Linked List, you would have to start at 1 and hop 78 times. In a Skip List, we start at the **highest level** (Level 2) and work down.

1. **Start at Level 2 (Express Lane):**
* You are at `Start`. You look right.
* You see `50`. Is `50 <= 78`? Yes. **Jump to 50.**
* From `50`, you look right. You see `End` (or null). That's too far.
* **Action:** Stay at `50` and **drop down** to Level 1.


2. **Move to Level 1 (Fast Lane):**
* You are currently at `50`. You look right.
* You see `75`. Is `75 <= 78`? Yes. **Jump to 75.**
* From `75`, you look right. You see `End`. That's too far.
* **Action:** Stay at `75` and **drop down** to Level 0.


3. **Move to Level 0 (Slow Lane):**
* You are currently at `75`. You look right.
* You see `76` (hidden in diagram). Hop to 76.
* You see `77`. Hop to 77.
* You see `78`. **Found it!**



**Result:** Instead of 78 hops, you took about **5 or 6 hops**.

---

### Scenario 2: Inserting Data (Insert `42`)

Now, let's say we want to insert the number **42**.

#### Step A: Find the Spot

First, the list performs a "Search" exactly like above to find where 42 *should* go. It searches for the largest number less than 42.

* It zooms through Level 2 to `Start`.
* It zooms through Level 1 to `25`.
* It zooms through Level 0 to `40`.
* It stops at `40`, because the next number is `50`, which is bigger than `42`.

#### Step B: Determine the Height (The "Coin Flip")

This is the magic part. The computer flips a virtual coin to decide how "tall" this new node will be.

* **Flip 1:** Heads. (Promote to Level 1).
* **Flip 2:** Heads. (Promote to Level 2).
* **Flip 3:** Tails. (Stop here).

So, our new node `42` will exist at **Level 0, Level 1, and Level 2**.

#### Step C: Update Pointers ("Sewing it in")

Now we just rewire the arrows (pointers) to weave `42` into the list. This happens locally.

**Before Insertion (Level 0):** `[40] -> [50]`
**After Insertion (Level 0):** `[40] -> [42] -> [50]`

**Before Insertion (Level 1):** `[25] -> [50]`
**After Insertion (Level 1):** `[25] -> [42] -> [50]`

**Before Insertion (Level 2):** `[Start] -> [50]`
**After Insertion (Level 2):** `[Start] -> [42] -> [50]`

*Because `42` won the coin toss twice, it became a "VIP" node that speeds up future searches for numbers like 43 or 45.*

---

### Why is this genius for LevelDB?

Imagine a thread is reading the list while another thread is inserting `42`.

If this were a **Tree**, inserting `42` might cause the whole tree to lean too far to the left, forcing a "rotation" (moving `40` up and `50` down). That rotation touches many nodes, so you have to lock the whole tree, forcing the reader to wait.

In the **Skip List**, inserting `42` only changed the arrows on `Start`, `25`, and `40`. The rest of the list (1-39, 60-100) was completely untouched. The reader thread can keep scanning elsewhere without ever knowing `42` was just added.

