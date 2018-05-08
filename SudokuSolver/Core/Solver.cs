﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;

namespace SudokuSolver.Core
{
    class Solver
    {
        Board board;

        string log = "";

        string[] fishStr = new string[] { "", "", "X-Wing", "Swordfish", "Jellyfish" };
        string[] tupleStr = new string[] { "", "single", "pair", "triple", "quadruple" };

        public Solver(int[][] inBoard, UI.SudokuBoard control)
        {
            control.SetBoard(board = new Board(inBoard));
        }

        void Log(string technique, string format, params object[] args) => Log(technique + "\t" + format, args);
        void Log(string format, params object[] args) => Log(string.Format(format, args));
        void Log(string s) => log += s + Environment.NewLine;

        public void DoWork(object sender, DoWorkEventArgs e)
        {
            bool changed, done;
            do
            {
                changed = false; // If this is true at the end of the loop, loop again
                done = true; // If this is true after a segment, the puzzle is solved and we can break

                Log("Loop");

                // Check for naked singles or a completed puzzle
                for (int x = 0; x < 9; x++)
                {
                    for (int y = 0; y < 9; y++)
                    {
                        Point p = new Point(x, y);
                        if (board[p] != 0) continue;

                        done = false;
                        // Check for naked singles
                        var a = board[p].Candidates.ToArray();
                        if (a.Length == 1)
                        {
                            board[p].Set(a[0]);
                            Log("Naked single", "{0}: {1}", p, a[0]);
                            changed = true;
                        }
                    }
                }
                if (done) { Log("Solver completed the puzzle."); break; }
                if (changed) continue;

                // Check for hidden singles
                for (int i = 0; i < 9; i++)
                {
                    foreach (Region[] r in Board.Regions)
                    {
                        for (int v = 1; v <= 9; v++)
                        {
                            Point[] p = r[i].GetPointsWithCandidate(v);
                            if (p.Length == 1)
                            {
                                board[p[0]].Set(v);
                                Log("Hidden single", "{0}: {1}", p[0], v);
                                changed = true;
                            }
                        }
                    }
                }
                if (changed) continue; // Do another pass with simple logic before moving onto more intensive logic

                // Check for naked pairs
                for (int i = 0; i < 9; i++)
                {
                    if (FindNaked(Board.Blocks[i], 2)
                        || FindNaked(Board.Rows[i], 2)
                        || FindNaked(Board.Columns[i], 2)) { changed = true; break; }
                }
                if (changed) continue;

                // Check for hidden pairs
                for (int i = 0; i < 9; i++)
                {
                    if (FindHidden(Board.Blocks[i], 2)
                        || FindHidden(Board.Rows[i], 2)
                        || FindHidden(Board.Columns[i], 2)) { changed = true; break; }
                }
                if (changed) continue;

                // Check for locked row/column candidates
                for (int i = 0; i < 9; i++)
                {
                    for (int v = 1; v <= 9; v++)
                    {
                        if (FindLocked(true, i, v) || FindLocked(false, i, v)) changed = true;
                    }
                }
                if (changed) continue;

                // Check for Y-Wings
                for (int i = 0; i < 9; i++)
                {
                    if (FindYWing(Board.Rows[i]) || FindYWing(Board.Columns[i])) { changed = true; break; }
                }
                if (changed) continue;

                // Check for pointing pairs/triples
                // For example: 
                // 9 3 6     0 5 0     7 0 4
                // 2 7 8     1 9 4     5 3 6
                // 0 0 5     0 7 0     9 0 0
                // The block on the left can only have 1s in the bottom row, so remove the possibility of 1s in the block on the right's bottom row
                // A 1 will then be placed in the top spot of that block on the next loop, because it is the only available spot for a 1
                // I did not make this a dedicated function because the loops would happen more than they already do
                for (int i = 0; i < 3; i++)
                {
                    Point[][] blockrow = new Point[3][], blockcol = new Point[3][];
                    for (int r = 0; r < 3; r++)
                    {
                        blockrow[r] = Board.Blocks[r + (i * 3)].Points;
                        blockcol[r] = Board.Blocks[i + (r * 3)].Points;
                    }
                    for (int r = 0; r < 3; r++) // 3 blocks in a blockrow/blockcolumn
                    {
                        int[][] rowCand = new int[3][], colCand = new int[3][];
                        for (int j = 0; j < 3; j++) // 3 rows/columns in block
                        {
                            // The 3 cells' candidates in a block's row/column
                            rowCand[j] = blockrow[r].GetRow(j).Select(p => board[p].Candidates).UniteAll().ToArray();
                            colCand[j] = blockcol[r].GetColumn(j).Select(p => board[p].Candidates).UniteAll().ToArray();
                        }
                        // Now check if a row has a distinct candidate
                        var zero_distinct = rowCand[0].Except(rowCand[1]).Except(rowCand[2]);
                        if (zero_distinct.Count() > 0)
                            if (RemoveBlockRowColCandidates(blockrow, true, i, r, 0, zero_distinct)) changed = true;
                        var one_distinct = rowCand[1].Except(rowCand[0]).Except(rowCand[2]);
                        if (one_distinct.Count() > 0)
                            if (RemoveBlockRowColCandidates(blockrow, true, i, r, 1, one_distinct)) changed = true;
                        var two_distinct = rowCand[2].Except(rowCand[0]).Except(rowCand[1]);
                        if (two_distinct.Count() > 0)
                            if (RemoveBlockRowColCandidates(blockrow, true, i, r, 2, two_distinct)) changed = true;
                        // Now check if a column has a distinct candidate
                        zero_distinct = colCand[0].Except(colCand[1]).Except(colCand[2]);
                        if (zero_distinct.Count() > 0)
                            if (RemoveBlockRowColCandidates(blockcol, false, i, r, 0, zero_distinct)) changed = true;
                        one_distinct = colCand[1].Except(colCand[0]).Except(colCand[2]);
                        if (one_distinct.Count() > 0)
                            if (RemoveBlockRowColCandidates(blockcol, false, i, r, 1, one_distinct)) changed = true;
                        two_distinct = colCand[2].Except(colCand[0]).Except(colCand[1]);
                        if (two_distinct.Count() > 0)
                            if (RemoveBlockRowColCandidates(blockcol, false, i, r, 2, two_distinct)) changed = true;
                    }
                }
                if (changed) continue;

                // Check for naked triples
                for (int i = 0; i < 9; i++)
                {
                    if (FindNaked(Board.Blocks[i], 3)
                        || FindNaked(Board.Rows[i], 3)
                        || FindNaked(Board.Columns[i], 3)) { changed = true; break; }
                }
                if (changed) continue;

                // Check for hidden triples
                for (int i = 0; i < 9; i++)
                {
                    if (FindHidden(Board.Blocks[i], 3)
                        || FindHidden(Board.Rows[i], 3)
                        || FindHidden(Board.Columns[i], 3)) { changed = true; break; }
                }
                if (changed) continue;

                // Check for X-Wings, Swordfish & Jellyfish
                if (FindFish(2) || FindFish(3) || FindFish(4)) { changed = true; continue; }

                // Check for naked quads
                for (int i = 0; i < 9; i++)
                {
                    if (FindNaked(Board.Blocks[i], 4)
                        || FindNaked(Board.Rows[i], 4)
                        || FindNaked(Board.Columns[i], 4)) { changed = true; break; }
                }
                if (changed) continue;

                // Check for hidden quads
                for (int i = 0; i < 9; i++)
                {
                    if (FindHidden(Board.Blocks[i], 4)
                        || FindHidden(Board.Rows[i], 4)
                        || FindHidden(Board.Columns[i], 4)) { changed = true; break; }
                }

            } while (changed);

            e.Result = log;
        }

        // Find Y-Wing
        bool FindYWing(Region region)
        {
            var points = region.Points.Where(p => board[p].Candidates.Count == 2).ToArray();
            if (points.Length > 1)
            {
                for (int j = 0; j < points.Length; j++)
                {
                    Point p1 = points[j];
                    for (int k = j + 1; k < points.Length; k++)
                    {
                        Point p2 = points[k];
                        var inter = board[p1].Candidates.Intersect(board[p2].Candidates).ToArray();
                        if (inter.Length != 1) continue;

                        var a = new int[] { inter[0] };
                        int other1 = board[p1].Candidates.Except(a).ToArray()[0],
                            other2 = board[p2].Candidates.Except(a).ToArray()[0];

                        var b = new Point[] { p1, p2 };
                        foreach (Point point in b)
                        {
                            var p3a = board[point].GetCanSeePoints().Except(points).Where(p => board[p].Candidates.Count == 2 && board[p].Candidates.Intersect(new int[] { other1, other2 }).Count() == 2).ToArray();
                            if (p3a.Length == 1) // Example: p1 and p3 see each other, so remove similarities from p2 and p3
                            {
                                Point p3 = p3a[0];
                                Point pOther = b.Single(p => p != point);
                                var common = board[pOther].GetCanSeePoints().Intersect(board[p3].GetCanSeePoints());
                                var cand = board[pOther].Candidates.Intersect(board[p3].Candidates).ToArray(); // Will just be 1 candidate
                                if (board.BlacklistCandidates(common, cand))
                                {
                                    Log("Y-Wing", "{0}: {1}", new Point[] { p1, p2, p3 }.Print(), cand[0]);
                                    return true;
                                }
                            }
                        }
                    }
                }
            }
            return false;
        }

        // Find X-Wing, Swordfish & Jellyfish
        bool FindFish(int amt)
        {
            for (int v = 1; v <= 9; v++)
            {
                if (DoFish(v, 0, amt, new int[amt])) return true;
            }
            return false;
        }
        bool DoFish(int cand, int loop, int amt, int[] indexes)
        {
            if (loop == amt)
            {
                Point[][] rowPoints = indexes.Select(i => Board.Rows[i].GetPointsWithCandidate(cand)).ToArray(),
                    colPoints = indexes.Select(i => Board.Columns[i].GetPointsWithCandidate(cand)).ToArray();

                IEnumerable<int> rowLengths = rowPoints.Select(parr => parr.Length),
                    colLengths = colPoints.Select(parr => parr.Length);

                if (rowLengths.Max() == amt && rowLengths.Min() > 0 && rowPoints.Select(parr => parr.Select(p => p.X)).UniteAll().Count() <= amt)
                {
                    var row2D = rowPoints.UniteAll();
                    if (board.BlacklistCandidates(row2D.Select(p => Board.Columns[p.X].Points).UniteAll().Except(row2D), new int[] { cand }))
                    {
                        Log(fishStr[amt], "{0}: {1}", row2D.Print(), cand);
                        return true;
                    }
                }
                if (colLengths.Max() == amt && colLengths.Min() > 0 && colPoints.Select(parr => parr.Select(p => p.Y)).UniteAll().Count() <= amt)
                {
                    var col2D = colPoints.UniteAll();
                    if (board.BlacklistCandidates(col2D.Select(p => Board.Rows[p.Y].Points).UniteAll().Except(col2D), new int[] { cand }))
                    {
                        Log(fishStr[amt], "{0}: {1}", col2D.Print(), cand);
                        return true;
                    }
                }
            }
            else
            {
                for (int i = loop == 0 ? 0 : indexes[loop - 1] + 1; i < 9; i++)
                {
                    indexes[loop] = i;
                    if (DoFish(cand, loop + 1, amt, indexes)) return true;
                }
            }
            return false;
        }

        bool FindLocked(bool doRows, int rc, int value)
        {
            var with = (doRows ? Board.Rows : Board.Columns)[rc].GetPointsWithCandidate(value);

            // Even if a block only has these candidates for this "k" value, it'd be slower to check that before cancelling "BlacklistCandidates"
            if (with.Count() == 3 || with.Count() == 2)
            {
                var blocks = with.Select(p => board[p].Block).Distinct().ToArray();
                if (blocks.Length == 1)
                    if (board.BlacklistCandidates(Board.Blocks[blocks[0]].Points.Except(with), new int[] { value }))
                    {
                        Log("Locked candidate", "{4} {0} locks block {1}, {2}: {3}", rc, blocks[0], with.Print(), value, doRows ? "Row" : "Column");
                        return true;
                    }
            }
            return false;
        }

        // Find hidden pairs/triples/quadruples
        bool FindHidden(Region region, int amt)
        {
            if (region.Points.Count(p => board[p].Candidates.Count > 0) == amt) // If there are only "amt" cells with candidates, we don't have to waste our time
                return false;
            return DoHidden(region, 0, amt, new int[amt]);
        }
        bool DoHidden(Region region, int loop, int amt, int[] cand)
        {
            if (loop == amt)
            {
                var points = cand.Select(c => region.GetPointsWithCandidate(c)).UniteAll().ToArray();
                if (points.Length != amt // There aren't "amt" cells for our tuple to be in
                    || points.Select(p => board[p].Candidates).UniteAll().Count() == amt // We already know it's a tuple (might be faster to skip this check, idk)
                    || cand.Any(v => !points.Any(p => board[p].Candidates.Contains(v)))) return false; // If a number in our combo doesn't actually show up in any of our cells
                if (board.BlacklistCandidates(points, Enumerable.Range(1, 9).Except(cand)))
                {
                    Log("Hidden " + tupleStr[amt], "{0}: {1}", points.Print(), cand.Print());
                    return true;
                }
            }
            else
            {
                for (int i = cand[loop == 0 ? loop : loop - 1] + 1; i <= 9; i++)
                {
                    cand[loop] = i;
                    if (DoHidden(region, loop + 1, amt, cand)) return true;
                }
            }
            return false;
        }

        // Find naked pairs/triples/quadruples
        bool FindNaked(Region region, int amt)
        {
            if (region.Points.Count(p => board[p].Candidates.Count > 0) == amt) // If there are only "amt" cells with candidates, we don't have to waste our time
                return false;
            return DoNaked(region, 0, amt, new Point[amt], new int[amt]);
        }
        bool DoNaked(Region region, int loop, int amt, Point[] points, int[] indexes)
        {
            if (loop == amt)
            {
                var combo = points.Select(p => board[p].Candidates).UniteAll().ToArray();
                if (combo.Length == amt)
                {
                    if (board.BlacklistCandidates(Enumerable.Range(0, 9).Except(indexes).Select(i => region.Points[i]), combo))
                    {
                        Log("Naked " + tupleStr[amt], "{0}: {1}", points.Print(), combo.Print());
                        return true;
                    }
                }
            }
            else
            {
                for (int i = loop == 0 ? 0 : indexes[loop - 1] + 1; i < 9; i++)
                {
                    Point p = region.Points[i];
                    if (board[p].Candidates.Count == 0) continue;
                    points[loop] = p;
                    indexes[loop] = i;
                    if (DoNaked(region, loop + 1, amt, points, indexes)) return true;
                }
            }
            return false;
        }

        // Clear candidates from a blockrow/blockcolumn and return true if something changed
        bool RemoveBlockRowColCandidates(Point[][] blockrcs, bool doRows, int current, int ignoreBlock, int rc, IEnumerable<int> cand)
        {
            bool changed = false;
            for (int i = 0; i < 3; i++)
            {
                if (i == ignoreBlock) continue;
                var rcs = doRows ? blockrcs[i].GetRow(rc) : blockrcs[i].GetColumn(rc);
                if (board.BlacklistCandidates(rcs, cand)) changed = true;
            }
            if (changed) Log("Pointing couple", "Starting in block{0} {1}'s block {2}, {0} {3}: {4}", doRows ? "row" : "column", current, ignoreBlock, rc, cand.Print());
            return changed;
        }
    }
}
