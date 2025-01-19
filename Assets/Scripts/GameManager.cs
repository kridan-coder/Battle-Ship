using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    public enum GameState {
        Init, Play, Finished
    }

    private bool setupComplete = false;
    private bool anotherPlayerReady = false;
    private bool isLoading = true;
    public GameObject sessionHUD;
    public GameObject menuHUD;
    public GameObject loadingHUD;

    public NetworkManager networkManager;

    [Header("Ships")]
    public GameObject[] ships;
    public EnemyScript enemyScript;
    private ShipScript shipScript;
    public List<int[]> EnemyShips;
    public List<int[]> PlayerShips = new List<int[]>{};
    private int shipIndex = 0;
    public List<TileScript> allTileScripts;    
    public List<GameObject> allTiles;    

    [Header("HUD")]
    public Button nextBtn;
    public Button rotateBtn;
    public Button replayBtn;
    public Button joinBtn;
    public Button createBtn;
    
    public Text topText;
    public Text playerShipText;
    public Text enemyShipText;

    [Header("Objects")]
    public GameObject missilePrefab;
    public GameObject enemyMissilePrefab;
    public GameObject firePrefab;
    public GameObject woodDock;
    private bool playerTurn = true;
    
    private List<GameObject> playerFires = new List<GameObject>();
    private List<GameObject> enemyFires = new List<GameObject>();
    
    private int enemyShipCount = 5;
    private int playerShipCount = 5;


    // Start is called before the first frame update
    void Start()
    {
        nextBtn.onClick.AddListener(() => NextShipClicked());
        rotateBtn.onClick.AddListener(() => RotateClicked());
        replayBtn.onClick.AddListener(() => ReplayClicked());
        joinBtn.onClick.AddListener(() => JoinClicked());
        createBtn.onClick.AddListener(() => CreateClicked());
        shipScript = ships[shipIndex].GetComponent<ShipScript>();
        sessionHUD.SetActive(false);
        menuHUD.SetActive(false);
        loadingHUD.SetActive(true);
        networkManager.Connect();
    }


    public void StartMainMenu() {
        loadingHUD.SetActive(false);
        sessionHUD.SetActive(false);
        menuHUD.SetActive(true);
    }

    public void StartNewGame() {
        isLoading = false;
        topText.text = "Place ships";
    }

    private void JoinClicked()
    {
        networkManager.FindMatch();
    }

    private void CreateClicked()
    {
        networkManager.MakeRoom();
    }

    private void NextShipClicked()
    {
        if (!shipScript.OnGameBoard())
        {
            shipScript.FlashColor(Color.red);
        } else
        {
            if(shipIndex <= ships.Length - 2)
            {
                shipIndex++;
                shipScript = ships[shipIndex].GetComponent<ShipScript>();
                shipScript.FlashColor(Color.yellow);
            }
            else
            {
                rotateBtn.gameObject.SetActive(false);
                nextBtn.gameObject.SetActive(false);
                woodDock.SetActive(false);
                topText.text = "Wait for player";
                setupComplete = true;
                for (int i = 0; i < ships.Length; i++) ships[i].SetActive(false);
                networkManager.IAmReady();
                
            }
        }
        
    }

    public void StartGamePrepare() {
        topText.text = "Almost ready";
        setShips();
    }

    public void StartGameSession() {
        rotateBtn.gameObject.SetActive(false);
        nextBtn.gameObject.SetActive(false);
        woodDock.SetActive(false);
        setupComplete = true;
        anotherPlayerReady = true;
        for (int i = 0; i < ships.Length; i++) ships[i].SetActive(false);

        if (networkManager.IsMyFirstTurn()) {
            EndEnemyTurn();
        } else {
            EndPlayerTurn();
        }
    }

    public void SetEnemyShips(List<int[]> ships) {
        EnemyShips = ships;
    }


    void setShips() {
        List<ShipScript> shipScripts = new List<ShipScript> {};
        //allTiles.Reverse();

        foreach (GameObject ship in ships) {
            shipScripts.Add(ship.GetComponent<ShipScript>());
        }
        Debug.Log("SHIPS");
        for (int i = 0; i < shipScripts.Count; i++) {
            int[] currentShip = new int[] {};
            for (int j = 0; j < shipScripts[i].touchTiles.Count; j++) {
                GameObject shipTouchedTile = shipScripts[i].touchTiles[j];
                for (int k = 0; k < allTiles.Count; k++) {
                    var currentTile = allTiles[k];

                    if (currentTile.name == shipTouchedTile.name) {
                        currentShip = currentShip.Concat(new int[] { k + 1 }).ToArray();
                    }
                }
            }
            Debug.Log($"[{string.Join(",", currentShip)}]");
            PlayerShips.Add(currentShip);
            
        }
        Debug.Log("SHIPS END");

        networkManager.SendPlayerShips(PlayerShips);
        
    }

    public void TileClicked(GameObject tile)
    {
        if (!isLoading) {
            if(setupComplete && playerTurn && anotherPlayerReady)
            {
                CreateAttack(tile);
            } else if (!setupComplete)
            {
                PlaceShip(tile);
                shipScript.SetClickedTile(tile);
            }
        }
    }

    void CreateAttack(GameObject tile) {
        Vector3 tilePos = tile.transform.position;
        tilePos.y += 15;
        playerTurn = false;
        Instantiate(missilePrefab, tilePos, missilePrefab.transform.rotation);

        string pattern = @"(?<=\().+?(?=\))";
        Regex rg = new Regex(pattern);
        int tileNumber = Int32.Parse(rg.Matches(tile.name).Cast<Match>().Select(m => m.Value).ToArray()[0]);
        networkManager.SendMissile(tileNumber - 1);
    }

    private void PlaceShip(GameObject tile)
    {
        shipScript = ships[shipIndex].GetComponent<ShipScript>();
        shipScript.ClearTileList();
        Vector3 newVec = shipScript.GetOffsetVec(tile.transform.position);
        ships[shipIndex].transform.localPosition = newVec;
    }

    void RotateClicked()
    {
        shipScript.RotateShip();
    }

    public void CheckHit(GameObject tile)
    {
        int tileNum = Int32.Parse(Regex.Match(tile.name, @"\d+").Value);
        int hitCount = 0;
        foreach(int[] tileNumArray in EnemyShips)
        {
            if (tileNumArray.Contains(tileNum))
            {
                for (int i = 0; i < tileNumArray.Length; i++)
                {
                    if (tileNumArray[i] == tileNum)
                    {
                        tileNumArray[i] = -5;
                        hitCount++;
                    }
                    else if (tileNumArray[i] == -5)
                    {
                        hitCount++;
                    }
                }
                if (hitCount == tileNumArray.Length)
                {
                    enemyShipCount--;
                    topText.text = "SUNK!!!!!!";
                    enemyFires.Add(Instantiate(firePrefab, tile.transform.position, Quaternion.identity));
                    tile.GetComponent<TileScript>().SetTileColor(1, new Color32(68, 0, 0, 255));
                    tile.GetComponent<TileScript>().SwitchColors(1);
                }
                else
                {
                    topText.text = "HIT!!";
                    tile.GetComponent<TileScript>().SetTileColor(1, new Color32(255, 0, 0, 255));
                    tile.GetComponent<TileScript>().SwitchColors(1);
                }
                break;
            }
            
        }
        if(hitCount == 0)
        {
            tile.GetComponent<TileScript>().SetTileColor(1, new Color32(38, 57, 76, 255));
            tile.GetComponent<TileScript>().SwitchColors(1);
            topText.text = "Missed, there is no ship there.";
        }
        Invoke("EndPlayerTurn", 1.0f);
    }

    public void EnemyHitPlayer(Vector3 tile, int tileNum, GameObject hitObj)
    {
        enemyScript.MissileHit(tileNum);
        tile.y += 0.2f;
        playerFires.Add(Instantiate(firePrefab, tile, Quaternion.identity));
        if (hitObj.GetComponent<ShipScript>().HitCheckSank())
        {
            playerShipCount--;
            playerShipText.text = playerShipCount.ToString();
            enemyScript.SunkPlayer();
        }
       Invoke("EndEnemyTurn", 2.0f);
    }
    
    public void SendAttackOn(int tile) {
        enemyScript.SendAttackOn(tile);
        
    }
    private void EndPlayerTurn()
    {
        for (int i = 0; i < ships.Length; i++) ships[i].SetActive(true);
        foreach (GameObject fire in playerFires) fire.SetActive(true);
        foreach (GameObject fire in enemyFires) fire.SetActive(false);
        enemyShipText.text = enemyShipCount.ToString();
        topText.text = "Enemy's turn";
        ColorAllTiles(0);
        if (playerShipCount < 1) GameOver("ENEMY WINs!!!");
    }

    public void EndEnemyTurn()
    {
        for (int i = 0; i < ships.Length; i++) ships[i].SetActive(false);
        foreach (GameObject fire in playerFires) fire.SetActive(false);
        foreach (GameObject fire in enemyFires) fire.SetActive(true);
        playerShipText.text = playerShipCount.ToString();
        topText.text = "Select a tile";
        playerTurn = true;
        ColorAllTiles(1);
        if (enemyShipCount < 1) GameOver("YOU WIN!!");
    }

    private void ColorAllTiles(int colorIndex)
    {
        foreach (TileScript tileScript in allTileScripts)
        {
            tileScript.SwitchColors(colorIndex);
        }
    }

    void GameOver(string winner)
    {
        topText.text = "Game Over: " + winner;
        replayBtn.gameObject.SetActive(true);
        playerTurn = false;
    }

    void ReplayClicked()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }


}
