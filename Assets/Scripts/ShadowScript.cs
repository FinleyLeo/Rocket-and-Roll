using System.Collections;
using UnityEngine;
using UnityEngine.Tilemaps;

public class ShadowScript : MonoBehaviour
{
    Vector3 offset = new Vector3(-0.2f, -0.2f);
    int layerOrder = -4;

    Renderer rendCast, rendShadow;
    Transform shadow;

    Material shadowMat;

    [SerializeField] RuleTile wallTile;
    Tilemap mainTM, shadowTM;

    private void Start()
    {
        shadow = new GameObject("Shadow").transform;
        shadow.parent = transform;
        shadow.localRotation = Quaternion.identity;
        shadow.localScale = Vector3.one;

        shadowMat = Resources.Load<Material>("ShadowMat");

        if (TryGetComponent<SpriteRenderer>(out SpriteRenderer sr))
        {
            rendCast = sr;
            rendShadow = shadow.gameObject.AddComponent<SpriteRenderer>();
        }
        else if (TryGetComponent<TrailRenderer>(out TrailRenderer tr))
        {
            rendCast = tr;
            rendShadow = shadow.gameObject.AddComponent<TrailRenderer>();
        }
        else if (TryGetComponent<TilemapRenderer>(out TilemapRenderer tmr))
        {
            mainTM = GetComponent<Tilemap>();
            shadowTM = shadow.gameObject.AddComponent<Tilemap>();
            TilemapRenderer _tmr = shadow.gameObject.AddComponent<TilemapRenderer>();

            _tmr.sortingOrder = layerOrder;
            _tmr.material = shadowMat;
        }

        if (rendShadow != null)
        {
            rendShadow.sortingOrder = layerOrder;
            rendShadow.material = shadowMat;
        }

        UpdateShadowTilemap();
    }

    public void UpdateShadowTilemap()
    {
        if (mainTM != null && shadowTM != null)
        {
            shadowTM.ClearAllTiles();

            var usedPositions = mainTM.cellBounds; // iterate bounds is often fastest & reliable

            for (int x = usedPositions.xMin; x <= usedPositions.xMax; x++)
            {
                for (int y = usedPositions.yMin; y <= usedPositions.yMax; y++)
                {
                    Vector3Int cell = new Vector3Int(x, y);
                    TileBase tile = mainTM.GetTile(cell);
                    if (tile == null) continue;

                    // copy tile
                    shadowTM.SetTile(cell, tile);
                }
            }

            shadowTM.RefreshAllTiles();
        }
    }

    private void LateUpdate()
    {
        shadow.position = transform.position + offset;

        if (rendShadow is SpriteRenderer sr && rendCast is SpriteRenderer _sr)
        {
            sr.sprite = _sr.sprite;
        }
    }
}
