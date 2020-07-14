﻿using System.Collections;
using Mirror;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Events;

namespace Insight {
	public class GameServerManager : InsightModule {
		private InsightClient client;
		private Transport transport;
		private NetworkManager netManager;
		
		private IEnumerator gameUpdater;

		//Pulled from command line arguments
		private int ownerId;
		private string uniqueId;
		private string networkAddress;
		private ushort networkPort;
		private string sceneName;
		private string gameName;
		private int minPlayers;
		private int maxPlayers;
		private int currentPlayers;
		private bool hasStarted;

		[SerializeField] private float updateDelayInSeconds = 1;

		public override void Initialize(InsightClient _client, ModuleManager _manager) {
			Debug.Log("[GameServerManager] - Initialization");
			client = _client;
			
			transport = Transport.activeTransport;
			netManager = NetworkManager.singleton;
			maxPlayers = netManager.maxConnections;

			GatherCmdArgs();
			StartClientWith(RegisterGame);
			
			RegisterHandlers();
			
			netManager.StartServer();
		}
		
		private void StartClientWith(ConnectionDelegate _handler) {
			if (client.IsConnected) {
				_handler.Invoke();
			}
			
			client.OnConnected += _handler;
		}

		private void RegisterHandlers() {
			client.OnDisconnected += Application.Quit;
			
			transport.OnServerConnected.AddListener(_ => GameUpdate());
			transport.OnServerDisconnected.AddListener(_ => GameUpdate());
		}

		private void GatherCmdArgs() {
			var args = new InsightArgs();

			if (args.IsProvided(ArgNames.UniqueId)) {
				Debug.Log("[Args] - UniqueID: " + args.UniqueId);
				uniqueId = args.UniqueId;
			}
			
			if (args.IsProvided(ArgNames.NetworkAddress)) {
				Debug.Log("[Args] - NetworkAddress: " + args.NetworkAddress);
				networkAddress = args.NetworkAddress;
				netManager.networkAddress = networkAddress;
			}

			if (args.IsProvided(ArgNames.NetworkPort)) {
				Debug.Log("[Args] - NetworkPort: " + args.NetworkPort);
				networkPort = (ushort) args.NetworkPort;

				if (transport.GetType().GetField("port") != null) {
					transport.GetType().GetField("port")
						.SetValue(transport, (ushort) args.NetworkPort);
				}
			}

			if (args.IsProvided(ArgNames.GameName)) {
				Debug.Log("[Args] - GameName: " + args.GameName);
				gameName = args.GameName;
			}
			
			if (args.IsProvided(ArgNames.MinPlayers)) {
				Debug.Log("[Args] - MinPlayers: " + args.MinPlayers);
				minPlayers = args.MinPlayers;
			}
		}

		#region Sender

		private void RegisterGame() {
			Assert.IsTrue(client.IsConnected);
			Debug.Log("[GameServerManager] - Registering game");
			
			client.NetworkSend(new RegisterGameMsg {
				uniqueId = uniqueId,
				networkAddress = networkAddress,
				networkPort = networkPort,
				gameName = gameName,
				minPlayers = minPlayers,
				maxPlayers = maxPlayers,
				currentPlayers = currentPlayers
			});
		}

		private void GameUpdate() {
			if (gameUpdater != null) StopCoroutine(gameUpdater);
			gameUpdater = GameUpdateCor();
			StartCoroutine(gameUpdater);
		}

		private IEnumerator GameUpdateCor() {
			while (!client.IsConnected) {
				yield return new WaitForSeconds(updateDelayInSeconds);
			}

			currentPlayers = NetworkServer.connections.Count;
			
			var startedText = hasStarted ? "started" : "not started";
			Debug.Log($"[GameServerManager] - Updating game : {currentPlayers} players in the {startedText} game");
			client.NetworkSend(new GameStatusMsg {
				uniqueId = uniqueId,
				currentPlayers = currentPlayers,
				hasStarted = hasStarted
			});
		}

		public void StartGame() {
			hasStarted = true;
			GameUpdate();
		}

		#endregion
	}
}