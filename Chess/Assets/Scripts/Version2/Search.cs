using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using Unity.VisualScripting;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Search = V2.Search;

namespace V2 {
    class Search
    {
        readonly MoveGen board;
        public bool DeadKing { get; set; }
        public bool Break { get; set; }
        public Search(BoardStruct board) 
        {
            this.board = new(board);
            this.board.SetSearchObject(this);
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
            if (depth == 0) {
                // Base case: Extended search depth exceeded
                return Evaluate.EvalBoard(board.IntBoard, board.WhiteToMove, false);
            }
            //if (depth < 1) {
            //    return Evaluate.EvalBoard(board.IntBoard, board.WhiteToMove, false);
            //}
    //
            List<Move> moves = board.PseudoLegalMoves();
            if (depth > 3) 
                moves = board.CullIllegalMoves(moves);

            foreach (Move m in moves) {
                if (depth < 1 && !m.IsNormalCapture) {
                    // Skip quiet moves (non-captures) if search depth is exceeded
                    continue;
                }
                oneMoveChecked = true;

                DeadKing = false;
                board.DoMove(m);
                int score = -SearchRec(depth-1, -beta, -alpha);
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
            List<Move> moves = board.LegalMoves();
            if (moves.Count == 0) return new(0);
            int highestScore = int.MinValue;
            Break = false;
            Move bestMove = new(0);
            foreach (Move m in moves) {
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