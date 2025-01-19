using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine.SceneManagement;
using System.Linq;
using ExitGames.Client.Photon;

public class NetworkManager : MonoBehaviourPunCallbacks, IOnEventCallback
{

    protected const byte UPDATE_PLAYER_READY = 1;

    protected const byte UPDATE_SHIPS_READY = 4;
    protected const byte MISSILE_SEND = 2;
    protected const byte SHIPS_SEND = 3;

    public GameObject sessionHUD;
    public GameObject menuHUD;

    public GameObject loadingHUD;
    public GameManager gameManager;

    public bool IsMyFirstTurn() {
        if (PhotonNetwork.CurrentRoom.PlayerCount > 1) {
            return PhotonNetwork.LocalPlayer.ActorNumber == PhotonNetwork.CurrentRoom.GetPlayer(1).ActorNumber;
        }
        Debug.Log("Not Enough Players");
        return false;
    }

    public void IAmReady()
    {
        // Do not use a new hashtable everytime but rather the existing
        // in order to not loose any other properties you might have later
        var hash = PhotonNetwork.LocalPlayer.CustomProperties;
        hash["Ready"] = true;   
        PhotonNetwork.LocalPlayer.SetCustomProperties(hash);

        Debug.Log("I am ready and my number is");
        Debug.Log(PhotonNetwork.LocalPlayer.ActorNumber);

        object[] content = new object[] { new Vector3(10.0f, 2.0f, 5.0f), 1, 2, 5, 10 };
        RaiseEventOptions raiseEventOptions = new RaiseEventOptions { Receivers = ReceiverGroup.All };
        PhotonNetwork.RaiseEvent(UPDATE_PLAYER_READY, content, raiseEventOptions, SendOptions.SendReliable);
    } 

    public void IHaveEnemyShips()
    {
        // Do not use a new hashtable everytime but rather the existing
        // in order to not loose any other properties you might have later
        var hash = PhotonNetwork.LocalPlayer.CustomProperties;
        hash["HaveShips"] = true;   
        PhotonNetwork.LocalPlayer.SetCustomProperties(hash);


        Debug.Log("Player number");
        Debug.Log(PhotonNetwork.LocalPlayer.ActorNumber);
        Debug.Log("Now has enemy ships");
        

        object[] content = new object[] { new Vector3(10.0f, 2.0f, 5.0f), 1, 2, 5, 10 };
        RaiseEventOptions raiseEventOptions = new RaiseEventOptions { Receivers = ReceiverGroup.All };
        PhotonNetwork.RaiseEvent(UPDATE_SHIPS_READY, content, raiseEventOptions, SendOptions.SendReliable);
    } 

    public void SendMissile(int index) {
        object[] content = new object[] { PhotonNetwork.LocalPlayer.ActorNumber, index };
        RaiseEventOptions raiseEventOptions = new RaiseEventOptions { Receivers = ReceiverGroup.All };
        PhotonNetwork.RaiseEvent(MISSILE_SEND, content, raiseEventOptions, SendOptions.SendReliable);
    }

    public void SendPlayerShips(List<int[]> ships)
    {
        object[] content = new object[] { PhotonNetwork.LocalPlayer.ActorNumber, ships.ToArray() };
        RaiseEventOptions raiseEventOptions = new RaiseEventOptions { Receivers = ReceiverGroup.All };
        PhotonNetwork.RaiseEvent(SHIPS_SEND, content, raiseEventOptions, SendOptions.SendReliable);
        
    }

    public void OnEvent(EventData photonEvent)
    {
        byte eventCode = photonEvent.Code;
        if (eventCode == UPDATE_PLAYER_READY) {
            CheckAllPlayersReady();
        } else if (eventCode == SHIPS_SEND) {
            object[] data = (object[])photonEvent.CustomData;
            int playerNumber = (int)data[0];
            int[][] shipsC = (int[][])data[1];
            List<int[]> ships = shipsC.ToList();
            if (playerNumber != PhotonNetwork.LocalPlayer.ActorNumber) {
               gameManager.SetEnemyShips(ships);
               IHaveEnemyShips();
            }
            
        } else if (eventCode == UPDATE_SHIPS_READY){
            CheckAllPlayersShipsReady();
        } else if (eventCode == MISSILE_SEND){
            object[] data = (object[])photonEvent.CustomData;
            int playerNumber = (int)data[0];
            int tile = (int)data[1];
            if (playerNumber != PhotonNetwork.LocalPlayer.ActorNumber) {
                gameManager.SendAttackOn(tile);
            }
        }
       
    }


    public void CheckAllPlayersReady()
    {
        var players = PhotonNetwork.PlayerList;
        
        // This is just using a shorthand via Linq instead of having a loop with a counter
        // for checking whether all players in the list have the key "Ready" in their custom properties
        if(players.All(p => p.CustomProperties.ContainsKey("Ready") && (bool)p.CustomProperties["Ready"]))
        {
            Debug.Log("All players are ready");
            gameManager.StartGamePrepare();
        }
    }

    public void CheckAllPlayersShipsReady ()
    {
        var players = PhotonNetwork.PlayerList;

        // This is just using a shorthand via Linq instead of having a loop with a counter
        // for checking whether all players in the list have the key "Ready" in their custom properties
        if(players.All(p => p.CustomProperties.ContainsKey("HaveShips") && (bool)p.CustomProperties["HaveShips"]))
        {
            Debug.Log("All players have enemy ships");
            gameManager.StartGameSession();
        }
    }

    public bool IsRoomFull()
	{
		return PhotonNetwork.CurrentRoom.PlayerCount == PhotonNetwork.CurrentRoom.MaxPlayers;
	}

    public void Connect()
    {
        PhotonNetwork.ConnectUsingSettings();
    }

    public override void OnConnectedToMaster()
    {
        PhotonNetwork.JoinLobby();
    }

    public override void OnJoinedLobby()
    {
        gameManager.StartMainMenu();
    }



    public override void OnJoinedRoom()
    {
        StartGame();
        
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
	{
        StartGame();
		Debug.LogError($"Player {newPlayer.ActorNumber} entered a room");
	}

    public void StartGame() {
        if (IsRoomFull()) {
            Debug.Log("Room is full, starting");
            menuHUD.SetActive(false);
            loadingHUD.SetActive(false);
		    sessionHUD.SetActive(true);
            gameManager.StartNewGame();
        }
    }

	public override void OnJoinRandomFailed(short returnCode,string message){
		Debug.Log("Cound not find room - creating a room");
		MakeRoom();
	}

    public void FindMatch() {
        Connect();
		menuHUD.SetActive(false);
		sessionHUD.SetActive(false);
        loadingHUD.SetActive(true);
		
		//Try to join a random room
		PhotonNetwork.JoinRandomRoom();
		Debug.Log("Searching for a random room");
		
	}

    public void MakeRoom() {
        Connect();
        menuHUD.SetActive(false);
		sessionHUD.SetActive(false);
        loadingHUD.SetActive(true);
		int randomRoomName = Random.Range(0,5000);
		RoomOptions roomOptions = new RoomOptions()
		{
			IsVisible = true,
			IsOpen = true,
			MaxPlayers = 2
		};
		PhotonNetwork.CreateRoom("RoomName_"+randomRoomName,roomOptions);
		Debug.Log("Room Created, Waiting For another Player");
	}
}
