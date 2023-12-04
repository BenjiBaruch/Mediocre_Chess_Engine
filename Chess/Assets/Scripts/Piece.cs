using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;


static class Piece
{
    /*
    Contains constants and methods for manipulating and getting information from piece integers.
    In C#, a static class is one that cannot be instantiated, has no constructor, and only contains
    static methods and static or constant variables

    Piece Integer Bit-wise Format: CCTTT where C is Color and T is Type
    All sliding pieces have 0b1?? type
    All straight sliding pieces (rook/queen) have 0b11? type
    All diagonal sliding pieces (bishop/queen) have 0b1?1 type
    All non-sliding pieces have 0b0?? type
    Type of 0 is null piece
    */

    // Piece codes:
    public const int Empty = 0b000;
    public const int King = 0b001;
    public const int Pawn = 0b010;
    public const int Knight = 0b011;
    public const int Bishop = 0b101;
    public const int Rook = 0b110;
    public const int Queen = 0b111;

    // Color codes:
    public const int White = 0b01000;
    public const int Black = 0b10000;

    // Commonly referenced pieces
    public const int WhitePawn = White | Pawn;
    public const int BlackPawn = Black | Pawn;
    public const int WhiteRook = White | Rook;
    public const int BlackRook = Black | Rook;

    // Masks determine which bits of move determine which information about pieces
    const int colorMask = 0b11000;
    const int typeMask = 0b00111;

    public static char Code(int piece) {
        return (piece & typeMask) switch {
            Empty => '_',
            King => 'K',
            Queen => 'Q',
            Rook => 'R',
            Knight => 'N',
            Bishop => 'B',
            Pawn => 'P',
            _ => '?'
        };
    }

    public static string ToString(int piece) {
        return (piece & colorMask) switch {
            White => "White ",
            Black => "Black ",
            _ => ""
        } + (piece & typeMask) switch {
            Empty => "nil",
            King => "King",
            Queen => "Queen",
            Rook => "Rook",
            Knight => "Knight",
            Bishop => "Bishop",
            Pawn => "Pawn",
            _ => "unknown"
        };
    }

    // Basic properties
    public static bool IsColor(int piece, int color) => (piece & colorMask) == color;

    public static bool IsType(int piece, int type) => (piece & typeMask) == type;

    public static int Color(int piece) => piece & colorMask;

    public static int Type(int piece) => piece & typeMask;

    // Movement properties
    public static bool CanSlide(int piece) => (piece & 0b100) > 0;

    public static bool CanSlideStraight(int piece) => (piece & Rook) == Rook;

    public static bool CanSlideDiagonal(int piece) => (piece & Bishop) == Bishop;
    public static string ColorToString(int color) => color == White ? "White" : "Black";
}