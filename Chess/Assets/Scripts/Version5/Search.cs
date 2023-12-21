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

namespace V5 
{
    public class Search
    {
        public MoveGen BoardObj { get; set; }
        public bool DeadKing { get; set; }
        public bool Break { get; set; }
        long moveGenTime;
        long moveCullTime;
        long TTAccessTime;
        long doMoveTime;
        long evalTime;
        int moveGenCount, moveCullCount, TTReadCount, TTWriteCount, doMoveCount, evalCount;
        Stopwatch stopwatch;
        public Zobrist Transposition { get; }
        public Search() 
        {
            Zobrist.Initialize();
            Transposition = new(27);
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
                int score = Evaluate.EvalBoard(BoardObj.IntBoard, BoardObj.WhiteToMove, false);
                evalCount++;
                stopwatch.Stop();
                evalTime += stopwatch.ElapsedMilliseconds;
                return score;
            }
            //if (depth < 1) {
            //    return Evaluate.EvalBoard(board.IntBoard, board.WhiteToMove, false);
            //}
    //
            stopwatch.Restart();
            PriorityQueue<Move, int> moves = BoardObj.PseudoLegalMoves();
            moveGenCount++;
            stopwatch.Stop();
            moveGenTime += stopwatch.ElapsedMilliseconds;
            stopwatch.Restart();
            if (depth > 3)  {
                moves = BoardObj.CullIllegalMoves(moves);
                moveCullCount++;
            }
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
                BoardObj.DoMove(m);
                doMoveCount += 2;
                stopwatch.Stop();
                doMoveTime += stopwatch.ElapsedMilliseconds;

                bool TTEntryFound = false;
                int score = 0;

                // Check TT
                stopwatch.Restart();
                int? value = Transposition.Read(BoardObj.Hash, depth);
                TTReadCount++;
                stopwatch.Stop();
                TTAccessTime += stopwatch.ElapsedMilliseconds;
                if (value != null) {
                    BoardObj.UndoMove();
                    return (int)value;
                }

                // Recur
                if (!TTEntryFound) {
                    score = -SearchRec(depth-1, -beta, -alpha);

                    // Write score to TT
                    stopwatch.Restart();
                    TTWriteCount++;
                    Transposition.Write(BoardObj.Hash, score, depth);
                    stopwatch.Stop();
                    TTAccessTime += stopwatch.ElapsedMilliseconds;
                }

                // Undo move
                stopwatch.Restart();
                BoardObj.UndoMove();
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
                int score = Evaluate.EvalBoard(BoardObj.IntBoard, BoardObj.WhiteToMove, false);
                stopwatch.Stop();
                evalTime += stopwatch.ElapsedMilliseconds;
                evalCount++;
                return score;
            }

            return alpha;
        }
        public Move BestMove(BoardStruct boardStruct, int depth) 
        {
            moveGenTime = moveCullTime = TTAccessTime = doMoveTime = evalTime = 0L;
            moveGenCount = moveCullCount = TTReadCount = TTWriteCount = doMoveCount = evalCount = 0;
            stopwatch = new();
            BoardObj = new(boardStruct);
            BoardObj.SetSearchObject(this);
            PriorityQueue<Move, int> moves = BoardObj.LegalMoves();
            if (moves.Count == 0) return new(0);
            int highestScore = int.MinValue;
            Break = false;
            Move bestMove = new(0);
            while (moves.Count > 0) {
                Move m = moves.Dequeue();
                DeadKing = false;
                BoardObj.DoMove(m);
                int score = -SearchRec(depth-1, int.MinValue/2, int.MaxValue/2);
                BoardObj.UndoMove();
                if (score > highestScore) {
                    highestScore = score;
                    bestMove = m;
                }
            }
            Debug.Log("moveGenTime: " + moveGenTime + 
                    "\nmoveCullTime: " + moveCullTime + 
                    "\nTTAccessTime: " + TTAccessTime + 
                    "\ndoMoveTime: " + doMoveTime +
                    "\nevalTime: " + evalTime +
                    "\nmoveGenCount: " + moveGenCount+ 
                    "\nmoveCullCount: " + moveCullCount+ 
                    "\nTTReadCount: " + TTReadCount+ 
                    "\nTTWriteCount: " + TTWriteCount+ 
                    "\ndoMoveCount: " + doMoveCount +
                    "\nevalCount: " + evalCount);
            return bestMove;
        }

        public List<Tuple<MoveGen, Move, long>> GenTestCases(List<Tuple<MoveGen, Move, long>> cases, int depth) {
            PriorityQueue<Move, int> moves = BoardObj.LegalMoves();
            if (moves.Count == 0)
                return cases;
            cases.Add(new(BoardObj.Clone(), moves.Dequeue(), BoardObj.Hash));
            if (depth == 0)
                return cases;
            while (moves.Count > 0) {
                Move m = moves.Dequeue();
                if (m.Value == 0) continue;
                DeadKing = false;
                BoardObj.DoMove(m);
                cases = GenTestCases(cases, depth-1);
                BoardObj.UndoMove();
            }
            return cases;
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