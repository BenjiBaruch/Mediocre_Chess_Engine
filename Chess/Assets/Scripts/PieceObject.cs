using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;

public class PieceObject : ScriptableObject
{
    GameObject instance;
    public int Piece { get; set; }
    // Help from https://gamedevbeginner.com/how-to-change-a-sprite-from-a-script-in-unity-with-examples
    // Help from https://gamedevbeginner.com/scriptable-objects-in-unity/
    SpriteRenderer SpriteRenderer;
    public Vector3 Coords { get; set; } 
    Vector3 prevCoords;
    public int Position { get; set; }
    

    public void SetParams(int position, int pieceCode, Sprite pieceSprite, GameObject instance) {
        // Help from  https://docs.unity3d.com/Manual/InstantiatingPrefabs.html
        
        // Stores object that renders piece in variable "instance"
        this.instance = instance;
        // Stores the object that renders that piece in "SpriteRenderer"
        SpriteRenderer = instance.GetComponent<SpriteRenderer>();
        // Sets object to be visible
        SpriteRenderer.enabled = true;
        // Sets piece image to inputted sprite
        SpriteRenderer.sprite = pieceSprite;
        // Sets position variables
        this.Position = position;
        Coords = prevCoords = instance.transform.position;
    }

    public int PlacePiece(Vector3 dropCoords) {
        tile = PositionFromCoords(dropCoords);
        if (tile == -1) return -1;
        Position = tile;
        Coords = CoordsFromPosition(tile);
        instance.transform.position = Coords;
        return Position;
    }

    public void ChangeSprite(Sprite pieceSprite) {
        SpriteRenderer.sprite = pieceSprite;
    }

    public void SetPosition(Vector3 position) {
        prevCoords = Coords;
        Coords = position;
        instance.transform.position = position;
    }

    public static Vector3 CoordsFromPosition(int position) {
        double x = (1.18971429 * (position % 8)) - 4.164;
        double y = (1.18842857 * (position / 8)) - 4.167;
        return new Vector3((float)x, (float)y, 0);
    }

    public static int PositionFromCoords(Vector3 coords) {
        int file = (int)Math.Round((coords.x + 4.164) * 6.72430353);
        int rank = (int)Math.Round((coords.y + 4.167) * 6.73157832);
        if (file < 0 || file > 7 || rank < 0 || rank > 7) {
            return -1;
        }
        return (rank * 8) + file;
    }
}
