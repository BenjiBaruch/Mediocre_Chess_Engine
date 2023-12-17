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
        readonly MoveGen board;
        public bool DeadKing { get; set; }
        public bool Break { get; set; }
        int iterations;
        Dictionary<long, Tuple<int, int>> TranspositionTable;
        public Search(BoardStruct board) 
        {
            Zobrist.Initialize();
            this.board = new(board);
            this.board.SetSearchObject(this);
            TranspositionTable = new(30000000);
        }
        public int SearchRec(int depth, int alpha, int beta) 
        {
            bool oneMoveChecked = false;
            if (Break) {
                return alpha;
            }

            if (iterations++ % 1000 == 0) {
                Debug.Log(iterations);
            }

            if (DeadKing) {
                // Base case: King captured
                return -999999;
            }

            if (TranspositionTable.ContainsKey(board.Hash)) {
                Tuple<int, int> entry = TranspositionTable[board.Hash];
                if (entry.Item2 >= depth)
                    return entry.Item1;
            }

            if (depth < 1) {
                // Base case: Extended search depth exceeded
                return Evaluate.EvalBoard(board.IntBoard, board.WhiteToMove, false);
            }
            //if (depth < 1) {
            //    return Evaluate.EvalBoard(board.IntBoard, board.WhiteToMove, false);
            //}
    //
            PriorityQueue<Move, int> moves = board.PseudoLegalMoves();
            if (depth > 3) 
                moves = board.CullIllegalMoves(moves);

            while (moves.Count > 0) {
                Move m = moves.Dequeue();
                if (depth < 1 && !m.IsNormalCapture) {
                    // Skip quiet moves (non-captures) if search depth is exceeded
                    continue;
                }
                oneMoveChecked = true;

                DeadKing = false;
                board.DoMove(m);
                int score = -SearchRec(depth-1, -beta, -alpha);
                TranspositionTable[board.Hash] = new(score, depth-1);
                board.UndoMove();

                // Help from https://www.chessprogramming.org/Alpha-Beta
                if (score >= beta)
                    return beta;
                if (score > alpha)
                    alpha = score;
            }

            if (!oneMoveChecked) {
                return Evaluate.EvalBoard(board.IntBoard, board.WhiteToMove, false);
            }

            return alpha;
        }
        public Move BestMove(int depth) 
        {
            PriorityQueue<Move, int> moves = board.LegalMoves();
            if (moves.Count == 0) return new(0);
            int highestScore = int.MinValue;
            Break = false;
            Move bestMove = new(0);
            iterations = 0;
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