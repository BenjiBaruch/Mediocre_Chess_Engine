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
            Transposition = new(27);
            sw = new();
        }
        public int SearchRec(int depth, int alpha, int beta, int extension, int consecutivePV) 
        {
            bool oneMoveChecked = false;

            if (sw.ElapsedMilliseconds > timeLimit) {
                timeLimitReached = true;
                return alpha;
            }

            if (Break) {
                return alpha;
            }

            if (DeadKing) {
                // Base case: King captured
                return -999999;
            }

            if (depth < 1) {
                // Base case: Extended search depth exceeded
                int score = Evaluate.EvalBoard(BoardObj.IntBoard, BoardObj.WhiteToMove, false);
                return score;
            }

            Move best = new(0);

            Tuple<Zobrist.Type, int> read = Transposition.Read(BoardObj.Hash, depth);
            switch (read.Item1) {
                case Zobrist.Type.PrevAlpha:
                    return read.Item2;
                case Zobrist.Type.HashMove:
                    best = new(read.Item2);
                    break;
            }

            iterations++;
    
            PriorityQueue<Move, int> moves = BoardObj.PseudoLegalMoves();
            if (depth > 3)  {
                moves = BoardObj.CullIllegalMoves(moves);
            }


            while (moves.Count > 0) {
                Move m = moves.Dequeue();
                if (depth < 1 && m.IsQuiet) {
                    // Skip quiet moves (non-captures) if search depth is exceeded
                    continue;
                }
                oneMoveChecked = true;
                DeadKing = false;

                // Do move
                BoardObj.DoMove(m);

                // Recur
                int score = -SearchRec(depth-1, -beta, -alpha, extension, 0);

                // Undo move
                BoardObj.UndoMove();

                // Update alpha-beta values
                // Help from https://www.chessprogramming.org/Alpha-Beta
                if (score >= beta) {
                    Transposition.Write(BoardObj.Hash, beta, depth, m.Value);
                    return beta;
                }
                if (score > alpha) {
                    alpha = score;
                    best = m;
                }
            }

            if (!oneMoveChecked) {
                int score = Evaluate.EvalBoard(BoardObj.IntBoard, BoardObj.WhiteToMove, false);
                return score;
            }

            Transposition.Write(BoardObj.Hash, alpha, depth, best.Value);

            return alpha;
        }

        bool SearchTo(int depth) {
            iterations = 0;
            PriorityQueue<Move, int> moves = BoardObj.LegalMoves();
            if (moves.Count == 0) return false;
            Break = false;
            int alpha = int.MinValue;
            if (principal.Value != 0) {
                BoardObj.DoMove(principal);
                alpha = -SearchRec(depth-1, 
                                         (int)(int.MinValue*0.5F), 
                                         (int)(int.MaxValue*0.5F), 
                                         0, 1);
                BoardObj.UndoMove();
            }
            while (moves.Count > 0) {
                if (timeLimitReached)
                    return false;
                Move m = moves.Dequeue();
                if (m.Value == principal.Value)
                    continue;
                DeadKing = false;

                BoardObj.DoMove(m);
                int score = -SearchRec(depth-1, 
                                       (int)(int.MinValue*0.5F), 
                                       -alpha, 
                                       0, 0);
                BoardObj.UndoMove();

                if (score > alpha) {
                    alpha = score;
                    principal = m;
                }
            }
            return true;
        }
        public Move BestMove(BoardStruct boardStruct, long timeLimit) 
        {
            var sw = Stopwatch.StartNew();
            this.timeLimit = timeLimit;
            BoardObj = new(boardStruct);
            BoardObj.SetSearchObject(this);
            PriorityQueue<Move, int> moves = BoardObj.LegalMoves();
            Transposition.ClearTable();
            Move best = new(0);
            int depth = 2;
            principal = new(0);
            int iterationsFinal = 0;
            while (sw.ElapsedMilliseconds < timeLimit) {
                bool success = SearchTo(depth++);
                if (success) {
                    best = principal;
                    iterationsFinal = iterations;
                }
            }
            Debug.Log("v6 Depth: " + depth + ", I: " + iterationsFinal);
            return best;
        }

        public int DeepEval(int depth) 
        {
            // Positive number means white is winning
            // Negative number means black is winning
            DeadKing = false;
            Break = false;
            return SearchRec(depth, int.MinValue/2, int.MaxValue/2, 0, 0);
        }
    }
}