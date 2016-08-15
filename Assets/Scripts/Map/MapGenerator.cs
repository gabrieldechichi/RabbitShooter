﻿using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class MapGenerator : MonoBehaviour
{
    public Map map;
    public int mapIndex;

    public Transform tilePrefab;
    public Transform wallPrefab;
    public Transform obstaclePrefab;
    public Transform navmeshFloor;
    public Transform navmeshMaskPrefab;
    public Transform boundary;

    public Vector2 maxMapSize;

    [Range(0, 1)]
    public float outlinePercent;

    List<Coord> allTileCoords;
    Queue<Coord> shuffledTileCoords;
    Queue<Coord> shuffledOpenTileCoords;
    Transform[,] tileMap;

    public float tileSize;


    void Awake()
    {
        Spawner spawner = FindObjectOfType<Spawner>();
        if (spawner!= null)
            FindObjectOfType<Spawner>().OnNewWave += OnNewWave;
        else
            GenerateMap();
    }

    public void GenerateMap()
    {
        tileMap = new Transform[map.mapSize.x, map.mapSize.y];
        GetComponent<BoxCollider>().size = new Vector3(map.mapSize.x * tileSize, .05f, map.mapSize.y * tileSize);



        allTileCoords = new List<Coord>();//Store all the x,y coordinates in our map
        //Store all coordinates
        for (int x = 0; x < map.mapSize.x; x++)
            for (int y = 0; y < map.mapSize.y; y++)
                allTileCoords.Add(new Coord(x, y));
        //Store all shuffled coordinates
        shuffledTileCoords = new Queue<Coord>(Utility.ShuffleArray(allTileCoords.ToArray(), map.seed));

        string holderName = "Generated Map";
        if (transform.FindChild(holderName))
            DestroyImmediate(transform.FindChild(holderName).gameObject);

        Transform mapHolder = new GameObject(holderName).transform;
        mapHolder.parent = transform;

        InstantiateTiles(mapHolder);
        InstantiateWalls(mapHolder);
        InstantiateObstacles(mapHolder);
        InstantiateBoundaries(mapHolder);
        InstantiateNavMask(mapHolder);

    }

    private void InstantiateNavMask(Transform mapHolder)
    {
        Transform maskLeft = Instantiate(navmeshMaskPrefab, Vector3.left * ((map.mapSize.x + maxMapSize.x) / 4f) * tileSize, Quaternion.identity) as Transform;
        maskLeft.parent = mapHolder;
        maskLeft.localScale = new Vector3((maxMapSize.x - map.mapSize.x) / 2f, 1, map.mapSize.y) * tileSize;

        Transform maskRight = Instantiate(navmeshMaskPrefab, Vector3.right * ((map.mapSize.x + maxMapSize.x) / 4f) * tileSize, Quaternion.identity) as Transform;
        maskRight.parent = mapHolder;
        maskRight.localScale = new Vector3((maxMapSize.x - map.mapSize.x) / 2f, 1, map.mapSize.y) * tileSize;

        Transform maskTop = Instantiate(navmeshMaskPrefab, Vector3.forward * ((map.mapSize.y + maxMapSize.y) / 4f) * tileSize, Quaternion.identity) as Transform;
        maskTop.parent = mapHolder;
        maskTop.localScale = new Vector3(maxMapSize.x, 1, (maxMapSize.y - map.mapSize.y) / 2f) * tileSize;

        Transform maskBottom = Instantiate(navmeshMaskPrefab, Vector3.back * ((map.mapSize.y + maxMapSize.y) / 4f) * tileSize, Quaternion.identity) as Transform;
        maskBottom.parent = mapHolder;
        maskBottom.localScale = new Vector3(maxMapSize.x, 1, (maxMapSize.y - map.mapSize.y) / 2f) * tileSize;

        navmeshFloor.localScale = new Vector3(maxMapSize.x, maxMapSize.y) * tileSize;//Navmesh floor = MaxMapsize
    }

    private void InstantiateBoundaries(Transform mapHolder)
    {
        Transform boundaryLeft = Instantiate(boundary, Vector3.left * ((map.mapSize.x + maxMapSize.x) / 4f) * tileSize, Quaternion.identity) as Transform;
        boundaryLeft.parent = mapHolder;
        boundaryLeft.GetComponent<BoxCollider>().size = new Vector3((maxMapSize.x - map.mapSize.x) / 2f, 1, map.mapSize.y) * tileSize;

        Transform boundaryRight = Instantiate(boundary, Vector3.right * ((map.mapSize.x + maxMapSize.x) / 4f) * tileSize, Quaternion.identity) as Transform;
        boundaryRight.parent = mapHolder;
        boundaryRight.GetComponent<BoxCollider>().size = new Vector3((maxMapSize.x - map.mapSize.x) / 2f, 1, map.mapSize.y) * tileSize;

        Transform boundaryTop = Instantiate(boundary, Vector3.forward * ((map.mapSize.y + maxMapSize.y) / 4f) * tileSize, Quaternion.identity) as Transform;
        boundaryTop.parent = mapHolder;
        boundaryTop.GetComponent<BoxCollider>().size = new Vector3(maxMapSize.x, 1, (maxMapSize.y - map.mapSize.y) / 2f) * tileSize;

        Transform boundaryBottom = Instantiate(boundary, Vector3.back * ((map.mapSize.y + maxMapSize.y) / 4f) * tileSize, Quaternion.identity) as Transform;
        boundaryBottom.parent = mapHolder;
        boundaryBottom.GetComponent<BoxCollider>().size = new Vector3(maxMapSize.x, 1, (maxMapSize.y - map.mapSize.y) / 2f) * tileSize;
    }

    private void InstantiateObstacles(Transform mapHolder)
    {
        System.Random prng = new System.Random(map.seed);

        bool[,] obstacleMap = new bool[(int)map.mapSize.x, (int)map.mapSize.y];
        List<Coord> allOpenCoords = new List<Coord>(allTileCoords);

        int obstacleCount = (int)(map.mapSize.x * map.mapSize.y * map.obstaclePercent);
        int currentObstacleCount = 0;

        for (int i = 0; i < obstacleCount; i++)
        {
            Coord randomCoord = GetRandomCoord();
            obstacleMap[randomCoord.x, randomCoord.y] = true;
            currentObstacleCount++;

            if (randomCoord != map.mapCentre && MapIsFullyAccessible(obstacleMap, currentObstacleCount))
            {
                float obstacleHeigh = Mathf.Lerp(map.minObstacleHeight, map.maxObstacleHeight, (float)prng.NextDouble());
                Vector3 obstaclePosition = CoordToPosition(randomCoord.x, randomCoord.y);

                Transform newObstacle = Instantiate(obstaclePrefab, obstaclePosition + Vector3.up * obstacleHeigh/2, Quaternion.identity) as Transform;
                newObstacle.localScale = new Vector3((1 - outlinePercent) * tileSize, obstacleHeigh, (1 - outlinePercent) * tileSize);
                newObstacle.parent = mapHolder;//So it gets destroyed with the rest of the map

                //Set the color of the obstacle
                Renderer obstacleRenderer = newObstacle.GetComponent<Renderer>();
                Material obstacleMaterial = new Material(obstacleRenderer.sharedMaterial);
                float colorPercent = randomCoord.y / (float)map.mapSize.y; //Interpolates based on y coordinate at the map
                obstacleMaterial.color = Color.Lerp(map.foregroundColor, map.backgroundColor, colorPercent);
                obstacleRenderer.sharedMaterial = obstacleMaterial;
                ///

                //remove this tile with obstacle from our alltilelist
                allOpenCoords.Remove(randomCoord);
            }
            else
            {
                obstacleMap[randomCoord.x, randomCoord.y] = false;
                currentObstacleCount--;
            }
        }

        shuffledOpenTileCoords = new Queue<Coord>(Utility.ShuffleArray(allOpenCoords.ToArray(), map.seed));
    }

    private void InstantiateTiles(Transform mapHolder)
    {
        for (int x = 0; x < map.mapSize.x; x++)
        {
            for (int y = 0; y < map.mapSize.y; y++)
            {
                Vector3 tilePosition = CoordToPosition(x, y);
                Transform newTile = Instantiate(tilePrefab, tilePosition, Quaternion.Euler(Vector3.right * 90)) as Transform;
                newTile.localScale = Vector3.one * (1 - outlinePercent) * tileSize;
                newTile.parent = mapHolder;
                tileMap[x, y] = newTile;
            }
        }
    }

    private void InstantiateWalls(Transform mapHolder)
    {
        for (int x = 0; x < map.mapSize.x; x++)
        {
            Vector3 wallPosition = CoordToPosition(x, map.mapSize.y - 1) + Vector3.up*3 + Vector3.forward*tileSize/2;
            Transform newWall = Instantiate(wallPrefab, wallPosition, Quaternion.identity) as Transform;
            newWall.localScale = new Vector3(tileSize, newWall.localScale.y, newWall.localScale.z);
            newWall.parent = mapHolder;
        }

        for (int x = 0; x < map.mapSize.x; x++)
        {
            Vector3 wallPosition = CoordToPosition(x, 0) + Vector3.up * 3 - Vector3.forward * tileSize / 2;
            Transform newWall = Instantiate(wallPrefab, wallPosition, Quaternion.identity) as Transform;
            newWall.localScale = new Vector3(tileSize, newWall.localScale.y, newWall.localScale.z);
            newWall.parent = mapHolder;
        }

        for (int y = 0; y < map.mapSize.y; y++)
        {
            Vector3 wallPosition = CoordToPosition(0, y) + Vector3.up * 3 - Vector3.right * tileSize / 2;
            Transform newWall = Instantiate(wallPrefab, wallPosition, Quaternion.Euler(0f,90f,0f)) as Transform;
            newWall.localScale = new Vector3(tileSize, newWall.localScale.y, newWall.localScale.z);
            newWall.parent = mapHolder;
        }

        for (int y = 0; y < map.mapSize.y; y++)
        {
            Vector3 wallPosition = CoordToPosition(map.mapSize.x - 1, y) + Vector3.up * 3 + Vector3.right * tileSize / 2;
            Transform newWall = Instantiate(wallPrefab, wallPosition, Quaternion.Euler(0f, -90f, 0f)) as Transform;
            newWall.localScale = new Vector3(tileSize, newWall.localScale.y, newWall.localScale.z);
            newWall.parent = mapHolder;
        }
    }

    void OnNewWave(int waveNumber)
    {
        mapIndex = waveNumber - 1;
        GenerateMap();
    }

    bool MapIsFullyAccessible(bool[,] obstacleMap, int currentObstacleCount)
    {
        bool[,] mapFlags = new bool[obstacleMap.GetLength(0), obstacleMap.GetLength(1)];//Keeps track of the already checked positions
        Queue<Coord> queue = new Queue<Coord>();
        queue.Enqueue(map.mapCentre);
        mapFlags[map.mapCentre.x, map.mapCentre.y] = true;//Is empty (accessible)

        int accessibleTileCount = 1;

        while (queue.Count > 0)
        {
            Coord tile = queue.Dequeue();

            for (int x = -1; x <= 1; x++)
            {
                for (int y = -1; y <= 1; y++)
                {
                    int neighbourX = tile.x + x;
                    int neighbourY = tile.y + y;
                    if (x == 0 || y == 0)
                    {
                        if (neighbourX >= 0 && neighbourX < obstacleMap.GetLength(0) && neighbourY >= 0 && neighbourY < obstacleMap.GetLength(1))
                        {//Guarantees it is inside the obstacle map
                            if (!mapFlags[neighbourX, neighbourY] && !obstacleMap[neighbourX, neighbourY])//We didn't check yet, and it's not an obstacle
                            {
                                mapFlags[neighbourX, neighbourY] = true;
                                queue.Enqueue(new Coord(neighbourX, neighbourY));
                                accessibleTileCount++;
                            }
                        }
                    }
                }
            }        
        }

        int targetAccessibleTileCount = (int)(map.mapSize.x * map.mapSize.y - currentObstacleCount);//All the tiles that are not obstacles should be accessible
        return targetAccessibleTileCount == accessibleTileCount;
    }

    public Vector3 CoordToPosition(int x, int y)
    {
        return new Vector3(-map.mapSize.x / 2f + 0.5f + x, 0, -map.mapSize.y / 2f + 0.5f + y) *tileSize;
    }

    public Coord GetRandomCoord()
    {
        Coord randomCoord = shuffledTileCoords.Dequeue();
        shuffledTileCoords.Enqueue(randomCoord);
        return randomCoord;
    }

    public Transform GetRandomOpenTile()
    {
        Coord randomCoord = shuffledOpenTileCoords.Dequeue();
        shuffledOpenTileCoords.Enqueue(randomCoord);
        return tileMap[randomCoord.x,randomCoord.y];
    }

    public Transform GetTileFromPosition(Vector3 position)
    {
        int x = Mathf.RoundToInt(position.x / tileSize + (map.mapSize.x - 1) / 2f);
        int y = Mathf.RoundToInt(position.z / tileSize + (map.mapSize.y - 1) / 2f);

        x = Mathf.Clamp(x, 0, tileMap.GetLength(0)-1);
        y = Mathf.Clamp(y, 0, tileMap.GetLength(1)-1);

        return tileMap[x, y];
    }

}

[System.Serializable]
public struct Coord
{
    public int x;
    public int y;

    public Coord(int _x, int _y)
    {
        x = _x;
        y = _y;
    }

    public static bool operator ==(Coord c1, Coord c2)
    {
        return c1.x == c2.x && c1.y == c2.y;
    }

    public static bool operator !=(Coord c1, Coord c2)
    {
        return !(c1 == c2);
    }

}

[System.Serializable]
public class Map
{
    public Coord mapSize;
    [Range(0, 1)]
    public float obstaclePercent;
    public int seed;
    public float minObstacleHeight;
    public float maxObstacleHeight;
    public Color foregroundColor;
    public Color backgroundColor;

    public Coord mapCentre
    {
        get
        {
            return new Coord(mapSize.x / 2, mapSize.y / 2);
        }
    }


}