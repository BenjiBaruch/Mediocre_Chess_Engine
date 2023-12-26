using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using UnityEngine;
using Search = V5.Search;
using Debug = UnityEngine.Debug;
using V5;

sealed class Version5 : ChessAbstract
{
    public override string Name { get; set; }
    public override string Version { get; set; }
    Search search;
    public override void Initialize(bool side)
    {
        Stopwatch watch = Stopwatch.StartNew();
        Side = side;
        search = new();
        Name = "Custom Transposition Table";
        Version = "5";
        watch.Stop();
        Debug.Log("v5 initialization time: " + watch.ElapsedMilliseconds);
    }
    public override Move GetMoveDrafted(BoardStruct b, int depth)
    {
        return search.BestMove(b, depth);
    }

    public override Move GetMoveTimed(BoardStruct b, long timeLimit)
    {
        throw new NotImplementedException();
    }

    public override double TimeFunc(string fName)
    {
        List<Tuple<MoveGen, Move, long>> cases = new(100000);
        MoveGen board = new(new Board().ToStruct());
        search.BoardObj = board;
        cases = search.GenTestCases(cases, 3, fName.Equals("read") || fName.Equals("read2"));
        Zobrist transposition = search.Transposition;
        Stopwatch sw = Stopwatch.StartNew();
        int i = 0;
        int stop = cases.Count;
        while (i < stop) {
            Tuple<MoveGen, Move, long> caseI = cases[i];
            i++;
            switch (fName) {
                case "eval":
                    Evaluate.EvalBoard(caseI.Item1.IntBoard, true, false);
                    break;
                case "write":
                    transposition.Write(caseI.Item3, 12351, 2);
                    break;
                case "read":
                    transposition.Read(caseI.Item3, 0);
                    break;
                case "write2":
                    transposition.Write2(caseI.Item3, 12351, 2);
                    break;
                case "read2":
                    transposition.Read2(caseI.Item3, 0);
                    break;
                case "gen pseudo":
                    caseI.Item1.PseudoLegalMoves();
                    break;
                case "gen legal":
                    caseI.Item1.LegalMoves();
                    break;
                case "do":
                    caseI.Item1.DoMove(caseI.Item2);
                    break;
            }
        }
        sw.Stop();
        return sw.ElapsedMilliseconds / (double)stop * 1_000_000D;
    }

    public override Dictionary<string, int> GetPartCounts()
    {
        Board b = new();
        b.DoMove(b.LegalMoves()[0]);
        b.DoMove(b.LegalMoves()[0]);
        b.DoMove(b.LegalMoves()[0]);
        b.DoMove(b.LegalMoves()[0]);
        return search.GetPartCounts(b.ToStruct(), 5);
    }
}