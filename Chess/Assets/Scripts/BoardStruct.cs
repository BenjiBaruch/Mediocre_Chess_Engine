public struct BoardStruct {
    public int[] IntBoard { get; set; }
    public bool WhiteToMove { get; set; }
    public int CastlingRights { get; set; }
    public int PawnLeapFile { get; set; }
    public int HalfmoveClock { get; set; }

    public BoardStruct(int[] intBoard, bool whiteToMove, int castlingRights, int pawnLeapFile, int halfmoveClock)
    {
        IntBoard = (int[]) intBoard.Clone();
        WhiteToMove = whiteToMove;
        CastlingRights = castlingRights;
        PawnLeapFile = pawnLeapFile;
        HalfmoveClock = halfmoveClock;
    }

}