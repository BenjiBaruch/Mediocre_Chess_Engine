using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;
using System.Numerics;
using Vector3 = UnityEngine.Vector3;
using UnityEngine.Rendering;

public class PieceObject : ScriptableObject
{
    /*
    This class essentially wraps Piece GameObjects, controlling their position and sprite
    */
    GameObject instance;
    public int PieceCode { get; set; }
    // Help from https://gamedevbeginner.com/how-to-change-a-sprite-from-a-script-in-unity-with-examples
    // Help from https://gamedevbeginner.com/scriptable-objects-in-unity/
    SpriteRenderer SpriteRenderer;
    public Vector3 Coords { get; set; } 
    Vector3 offset;
    public int Position { get; set; }
    

    public void SetParams(int position, int pieceCode, Sprite pieceSprite, GameObject instance) {
        // Help from  https://docs.unity3d.com/Manual/InstantiatingPrefabs.html
        
        PieceCode = pieceCode;
        // Stores object that renders piece in variable "instance"
        this.instance = instance;
        // Stores the object that renders that piece in "SpriteRenderer"
        SpriteRenderer = instance.GetComponent<SpriteRenderer>();
        // Sets object to be visible
        SpriteRenderer.enabled = true;
        // Sets piece image to inputted sprite
        SpriteRenderer.sprite = pieceSprite;
        // Sets position variables
        Position = position;
        Coords = instance.transform.position;
    }

    public int PlacePiece(Vector3 dropCoords) {
        // Gets index of tile to place piece
        int tile = PositionFromCoords(dropCoords);
        // If the coords are not on the board, don't place the tile
        if (tile == -1) return RejectPlacement();
        // Otherwise, update position and coords variables with new position
        Position = tile;
        Coords = CoordsFromPosition(tile);
        // The offset makes it so the piece moves towards the tile instead of just snapping to it.
        offset = Coords - dropCoords;
        return Position;
    }

    public int PlacePiece(Vector3 dropCoords, PieceObject[] others) {
        // Same as before, but with a foreach loop to check if the new tile index overlaps with other
        // pieces in others array, and rejects placement if so.
        int tile = PositionFromCoords(dropCoords);
        if (tile == -1) return RejectPlacement();
        foreach (PieceObject p in others)
            if (p.Position == tile)
                return RejectPlacement();
        Position = tile;
        Coords = CoordsFromPosition(tile);
        offset = Coords - dropCoords;
        return Position;
    }

    public int RejectPlacement() {
        // Sets Coords to the position the tile was at before it was dragged
        Vector3 real = CoordsFromPosition(Position);
        offset = real - Coords;
        Coords = real;
        return Position;
    }

    public void MoveTo(int position) {
        Position = position;
        offset = CoordsFromPosition(position) - Coords;
        Coords += offset;
    }

    public void Kill(int side, int index) {
        offset = Coords;
        if (side == Piece.White) {
            Coords = new Vector3(5.61F, -4.167F + 0.6F * index, -index);
        }
        else {
            Coords = new Vector3(-5.61F, 4.167F - 0.6F * index, -index);
        }
        offset = Coords - offset;
    }

    public void Update() {
        // Sets instance position in game world to abstract position
        instance.transform.position = Coords - offset;
        // Gradually decreases offset to make piece slowly move towards its location 
        // instead of juts snapping to it.
        offset = Vector3.Scale(offset, PieceManager.MouseGravity);
    }

    public void ChangeSprite(Sprite pieceSprite) {
        // Sets the piece image on the board to inputted Sprite object
        SpriteRenderer.sprite = pieceSprite;
    }

    public void SetPosition(Vector3 position) {
        offset = new(0, 0, 0);
        Coords = position;
        instance.transform.position = position;
    }

    public void SetPosition(Vector3 position, Vector3 offset) {
        this.offset = offset;
        Coords = position;
        instance.transform.position = position - offset;
    }

    public static Vector3 CoordsFromPosition(int position) {
        // Turns tile index in board representation into piece coordinates in game world
        double x = (1.18971429 * (position % 8)) - 4.164;
        double y = (1.18842857 * (position / 8)) - 4.167;
        return new Vector3((float)x, (float)y, 0);
    }

    public static int PositionFromCoords(Vector3 coords) {
        // Turns piece coordinates in game world into tile index in board prepresentation
        int file = (int)Math.Round((coords.x + 4.164) * 0.840537941);
        int rank = (int)Math.Round((coords.y + 4.167) * 0.84144729);
        // Debug.Log("file: " + file + ", rank: " + rank);
        if (file < 0 || file > 7 || rank < 0 || rank > 7) {
            return -1;
        }
        return (rank * 8) + file;
    }

    public override string ToString() {
        return Piece.ToString(PieceCode);
    }
}
