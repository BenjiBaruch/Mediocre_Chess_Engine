using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PieceObject : ScriptableObject
{
    GameObject instance;
    public int Piece { get; set; }
    // Help from https://gamedevbeginner.com/how-to-change-a-sprite-from-a-script-in-unity-with-examples
    // Help from https://gamedevbeginner.com/scriptable-objects-in-unity/
    SpriteRenderer SpriteRenderer;
    float x, y, prev_x, prev_y;
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
        x = prev_x = instance.transform.position.x;
        y = prev_y = instance.transform.position.y;
    }

    public void ChangeSprite(Sprite pieceSprite) {
        SpriteRenderer.sprite = pieceSprite;
    }

    public static Vector3 CoordsFromPosition(int position) {
        double x = (1.18971429 * (position % 8)) - 4.164;
        double y = (1.18842857 * (position / 8)) - 4.167;
        return new Vector3((float)x, (float)y, 0);
    }
}
