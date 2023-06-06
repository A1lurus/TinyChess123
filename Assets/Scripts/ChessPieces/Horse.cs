using System.Collections.Generic;
using UnityEngine;

public class Horse : ChessPiece
{
    public override List<Vector2Int> GetAvailableMoves(ref ChessPiece[,] board, int tileCountX, int tileCountY)
    {
        List<Vector2Int> r = new List<Vector2Int>();

        int[] xs = { 1, 2, 1, 2, -1, -2, -1, -2 };
        int[] ys = { 2, 1, -2, -1, 2, 1, -2, -1 };
        for (int i = 0; i < xs.Length; i++)
        {
            int x = currentX + xs[i];
            int y = currentY + ys[i];
            if (x >= 0 && x < tileCountX && y >= 0 && y < tileCountY)
            {
                if (board[x, y] == null || board[x, y].team != team)
                {
                    r.Add(new Vector2Int(x, y));
                }
            }
        }
        return r;
    }
}
