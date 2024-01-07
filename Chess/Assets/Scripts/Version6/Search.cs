using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using Unity.Collections;
using Unity.VisualScripting;
using UnityEngine;
using Utils;
using Debug = UnityEngine.Debug;

namespace V6 
{
    public class Search
    {
        public MoveGen BoardObj { get; set; }
        public bool DeadKing { get; set; }
        public bool Break { get; set; }
        bool timeLimitReached;
        long timeLimit;
        int iterations;
        readonly Stopwatch sw; 
        public Zobrist Transposition { get; }
        Move principal;
        public Search() 
        {
            Zobrist.Initialize();
            PrecomputeMoves.ComputeMoveTables();
            Transposition = new(26);
            sw = new();
        }
        public int SearchRec(int depth, int alpha, int beta, int consecutivePV) 
        {
            bool oneMoveChecked = false;

            if (sw.ElapsedMilliseconds > timeLimit) {
                // Base case: time limit reached
                timeLimitReached = true;
                return alpha;
            }

            if (Break) {
                // Base case: error in other script
                return alpha;
            }

            if (DeadKing) {
                // Base case: King captured
                return -999999;
            }

            if (consecutivePV > 1) {
                // If two hash moves were searched in a row, extend search depth by 1
                consecutivePV = 0;
                depth++;
            }

            if (depth < 1) {
                // Base case: Extended search depth exceeded
                int score = Evaluate.EvalBoard(BoardObj.IntBoard, BoardObj.WhiteToMove, false);
                return score;
            }

            Move best = new(0);

            // Read transposition table entry
            Tuple<Zobrist.Type, int> read = Transposition.Read(BoardObj.Hash, depth);
            switch (read.Item1) {
                case Zobrist.Type.PrevAlpha:
                    // If position was already searched at this draft,
                    // return previously discovered value
                    return read.Item2;
                case Zobrist.Type.HashMove:
                    // If position was already searched, but with a worse draft,
                    // First search what it considered to be the best move (hash move)
                    best = new(read.Item2);
                    break;
            }

            // Search hash move first
            if (best.Value != 0 && !(depth < 1 && best.IsQuiet)) {
                // Basically the same as the search block in the while loop (below)
                // More extensive notes over there                
                BoardObj.DoMove(best);
                int score = -SearchRec(depth-1, -beta, -alpha, consecutivePV + 1);
                BoardObj.UndoMove(best);
                if (score >= beta)
                    return beta;
                if (score > alpha)
                    alpha = score;
            }

            iterations++;

            // Generate moves if hash move wasn't precise enough
            PriorityQueue<Move, int> moves = BoardObj.PseudoLegalMoves();
            if (depth > 3)  {
                // If we're far from leaf nodes, use more precise move generation
                moves = BoardObj.CullIllegalMoves(moves);
            }

            int maxScore = int.MinValue;

            // Search all moves
            while (moves.Count > 0) {
                Move m = moves.Dequeue();

                if (m.Value == best.Value)
                    // Don't re-search hash move
                    continue;

                if (depth < 1 && m.IsQuiet) {
                    // Skip quiet moves (non-captures) if initial search depth is exceeded
                    continue;
                }
                oneMoveChecked = true;
                DeadKing = false;

                // Do move
                BoardObj.DoMove(m);

                // Recur
                int score = -SearchRec(depth-1, -beta, -alpha, 0);

                // Undo move
                BoardObj.UndoMove(m);

                // Update alpha-beta values
                // Help from https://www.chessprogramming.org/Alpha-Beta
                if (score >= beta) {
                    // If score exceeds beta (upper bound), it "fails high" and a reasonable opponent would never play it
                    // Therefore, we should record it to TT and return that upper bound
                    Transposition.Write(BoardObj.Hash, beta, depth, m.Value);
                    return beta;
                }
                if (score > alpha) {
                    // If score is greater than alpha (lower bound), 
                    // set alpha to score to narrow the window
                    alpha = score;
                }
                if (score > maxScore) {
                    // If score is best found so far, update best move
                    // If this position is searched again in the future,
                    // The best move will have been hashed to the TT and searched first
                    maxScore = score;
                    best = m;
                }
            }

            if (!oneMoveChecked) {
                // If no moves were searched (i.e. all moves were quiet)
                // Just eval board and return it
                int score = Evaluate.EvalBoard(BoardObj.IntBoard, BoardObj.WhiteToMove, false);
                return score;
            }

            // Write data to Transposition table
            Transposition.Write(BoardObj.Hash, alpha, depth, best.Value);

            return alpha;
        }

        bool SearchTo(int depth) {
            iterations = 0;
            PriorityQueue<Move, int> moves = BoardObj.LegalMoves();
            if (moves.Count == 0) {
                Debug.Log("no legal moves");
                return false;
            }
            Break = false;
            int alpha = int.MinValue/2;
            if (principal.Value != 0) {
                BoardObj.DoMove(principal);
                alpha = -SearchRec(depth-1, int.MinValue/2, int.MaxValue/2, 1);
                BoardObj.UndoMove(principal);
            }
            while (moves.Count > 0) {
                if (timeLimitReached) {
                    // Debug.Log("time limit");
                    return false;
                }
                Move m = moves.Dequeue();
                // Debug.Log("m: " + m.ToString);
                if (m.Value == principal.Value)
                    continue;
                DeadKing = false;

                BoardObj.DoMove(m);
                int score = -SearchRec(depth-1, int.MinValue/2, -alpha, 0);
                BoardObj.UndoMove(m);

                if (score > alpha) {
                    alpha = score;
                    principal = m;
                }
            }
            return true;
        }
        public Move BestMove(BoardStruct boardStruct, long timeLimit) 
        {
            this.timeLimit = timeLimit;
            Debug.Log(boardStruct.ToString());
            BoardObj = new(boardStruct);
            BoardObj.SetSearchObject(this);
            Transposition.ClearTable();
            timeLimitReached = false;
            sw.Restart();
            Move best = new(0);
            int depth = 2;
            principal = new(0);
            int iterationsFinal = 0;
            while (sw.ElapsedMilliseconds < timeLimit) {
                bool success = SearchTo(depth++);
                if (!BoardObj.ToStruct().Equals(boardStruct)) {
                    Debug.Log("DISCONTINUOUS:\n" + BoardObj.ToStruct() + '\n' + boardStruct);
                }
                if (success) {
                    best = principal;
                    iterationsFinal = iterations;
                }
                else {
                    break;
                }
            }
            sw.Stop();
            Debug.Log("v6 Depth: " + depth + ", I: " + iterationsFinal + ", m:" + best.ToString + ", t:" + sw.ElapsedMilliseconds);
            return best;
        }

        public Move BestMoveToDepth(BoardStruct boardStruct, int depth) {
            BoardObj = new(boardStruct);
            BoardObj.SetSearchObject(this);
            Transposition.ClearTable();
            principal = new(0);
            for (int i = 2; i < depth; i++) {
                SearchTo(i);
            }
            SearchTo(depth);
            Debug.Log("v6 I: " + iterations);
            return principal;
        }

        public int DeepEval(int depth) 
        {
            // Positive number means white is winning
            // Negative number means black is winning
            DeadKing = false;
            Break = false;
            return SearchRec(depth, int.MinValue/2, int.MaxValue/2, 0);
        }
    }
}