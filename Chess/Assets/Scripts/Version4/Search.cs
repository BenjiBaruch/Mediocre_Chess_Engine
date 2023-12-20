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

namespace V4 {
    public class Search
    {
        MoveGen board;
        public bool DeadKing { get; set; }
        public bool Break { get; set; }
        readonly Dictionary<long, Tuple<int, int>> TranspositionTable;
        long moveGenTime;
        long moveCullTime;
        long TTAccessTime;
        long doMoveTime;
        long evalTime;
        Stopwatch stopwatch;
        public Search() 
        {
            Zobrist.Initialize();
            TranspositionTable = new(100000000);
        }
        public int SearchRec(int depth, int alpha, int beta) 
        {
            bool oneMoveChecked = false;
            if (Break) {
                return alpha;
            }

            if (DeadKing) {
                // Base case: King captured
                return -999999;
            }

            if (depth < 1) {
                // Base case: Extended search depth exceeded
                stopwatch.Restart();
                int score = Evaluate.EvalBoard(board.IntBoard, board.WhiteToMove, false);
                stopwatch.Stop();
                evalTime += stopwatch.ElapsedMilliseconds;
                return score;
            }
            //if (depth < 1) {
            //    return Evaluate.EvalBoard(board.IntBoard, board.WhiteToMove, false);
            //}
    //
            stopwatch.Restart();
            PriorityQueue<Move, int> moves = board.PseudoLegalMoves();
            stopwatch.Stop();
            moveGenTime += stopwatch.ElapsedMilliseconds;
            stopwatch.Restart();
            if (depth > 3) 
                moves = board.CullIllegalMoves(moves);
            stopwatch.Stop();
            moveCullTime += stopwatch.ElapsedMilliseconds;


            while (moves.Count > 0) {
                Move m = moves.Dequeue();
                if (depth < 1 && !m.IsNormalCapture) {
                    // Skip quiet moves (non-captures) if search depth is exceeded
                    continue;
                }
                oneMoveChecked = true;

                DeadKing = false;

                // Do move
                stopwatch.Restart();
                board.DoMove(m);
                stopwatch.Stop();
                doMoveTime += stopwatch.ElapsedMilliseconds;

                bool TTEntryFound = false;
                int score = 0;

                // Check TT
                stopwatch.Restart();
                if (TranspositionTable.ContainsKey(board.Hash)) {
                    Tuple<int, int> entry = TranspositionTable[board.Hash];
                    if (entry.Item2 >= depth) {
                        score = entry.Item1;
                        TTEntryFound = true;
                    }
                }
                stopwatch.Stop();
                TTAccessTime += stopwatch.ElapsedMilliseconds;

                // Recur
                if (!TTEntryFound) {
                    score = -SearchRec(depth-1, -beta, -alpha);

                    // Write score to TT
                    stopwatch.Restart();
                    TranspositionTable[board.Hash] = new(score, depth-1);
                    stopwatch.Stop();
                    TTAccessTime += stopwatch.ElapsedMilliseconds;
                }

                // Undo move
                stopwatch.Restart();
                board.UndoMove();
                stopwatch.Stop();
                doMoveTime += stopwatch.ElapsedMilliseconds;

                // Update alpha-beta values
                // Help from https://www.chessprogramming.org/Alpha-Beta
                if (score >= beta)
                    return beta;
                if (score > alpha)
                    alpha = score;
            }

            if (!oneMoveChecked) {
                stopwatch.Restart();
                int score = Evaluate.EvalBoard(board.IntBoard, board.WhiteToMove, false);
                stopwatch.Stop();
                evalTime += stopwatch.ElapsedMilliseconds;
                return score;
            }

            return alpha;
        }
        public Move BestMove(BoardStruct boardStruct, int depth) 
        {
            moveGenTime = 0;
            moveCullTime = 0;
            TTAccessTime = 0;
            doMoveTime = 0;
            evalTime = 0;
            stopwatch = new();
            board = new(boardStruct);
            board.SetSearchObject(this);
            PriorityQueue<Move, int> moves = board.LegalMoves();
            if (moves.Count == 0) return new(0);
            int highestScore = int.MinValue;
            Break = false;
            Move bestMove = new(0);
            while (moves.Count > 0) {
                Move m = moves.Dequeue();
                DeadKing = false;
                board.DoMove(m);
                int score = -SearchRec(depth-1, int.MinValue/2, int.MaxValue/2);
                board.UndoMove();
                if (score > highestScore) {
                    highestScore = score;
                    bestMove = m;
                }
            }
            // Debug.Log("moveGenTime: " + moveGenTime + 
            //         "\nmoveCullTime: " + moveCullTime + 
            //         "\nTTAccessTime: " + TTAccessTime + 
            //         "\ndoMoveTime: " + doMoveTime +
            //         "\nevalTime: " + evalTime);
            return bestMove;
        }
        public int DeepEval(int depth) 
        {
            // Positive number means white is winning
            // Negative number means black is winning
            DeadKing = false;
            Break = false;
            return SearchRec(depth, int.MinValue/2, int.MaxValue/2);
        }
    }
}