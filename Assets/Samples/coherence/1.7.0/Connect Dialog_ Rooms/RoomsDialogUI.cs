// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Coherence.Cloud;
using Coherence.Connection;
using Coherence.Toolkit;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Coherence.Samples.RoomsDialog
{
    using Runtime;
    using UnityEngine.EventSystems;

    public class RoomsDialogUI : MonoBehaviour
    {
        #region References
        [Header("References")]
        public GameObject connectDialog;
        public GameObject disconnectDialog;
        public GameObject createRoomPanel;
        public GameObject regionSection;
        public GameObject noRSPlaceholder;
        public GameObject noCloudPlaceholder;
        public GameObject noRoomsAvailable;
        public GameObject loadingSpinner;
        public GameObject windowContent;
        public Font boldFont;
        public Font normalFont;
        public Text cloudText;
        public Text lanText;
        public Text joinRoomTitleText;
        public Text errorText;
        public ConnectDialogRoomView templateRoomView;
        public InputField roomNameInputField;
        public Toggle lanOnlineToggle;
        public InputField roomLimitInputField;
        public Dropdown regionDropdown;
        public Button refreshRegionsButton;
        public Button refreshRoomsButton;
        public Button joinRoomButton;
        public Button showCreateRoomPanelButton;
        public Button hideCreateRoomPanelButton;
        public Button createAndJoinRoomButton;
        public Button disconnectButton;
        public GameObject popupDialog;
        public Text popupText;
        public Text popupTitleText;
        public Button popupDismissButton;
        #endregion

        private IRoomsService selectedRoomService;
        private ReplicationServerRoomsService replicationServerRoomsService;
        private PlayerAccount playerAccount;
        private string initialJoinRoomTitle;
        private ListView roomsListView;
        private bool joinNextCreatedRoom;
        private ulong lastCreatedRoomUid;
        private Coroutine localToggleRefresher;
        private Coroutine cloudServiceReady;
        private CoherenceBridge bridge;

        private int RoomMaxPlayers => int.TryParse(roomLimitInputField.text, out var limit) ? limit : 10;
        private CloudRooms CloudRooms => bridge.CloudService?.Rooms;

        #region Unity Events
        private void OnEnable()
        {
            if (!bridge && !CoherenceBridgeStore.TryGetBridge(gameObject.scene, out bridge))
            {
                Debug.LogError($"{nameof(CoherenceBridge)} required on the scene.\n" +
                               "Add one via 'GameObject > coherence > Coherence Bridge'.");
                return;
            }

            if (!FindAnyObjectByType<EventSystem>())
            {
                Debug.LogError($"{nameof(EventSystem)} required on the scene.\n" +
                               "Add one via 'GameObject > UI > Event System'.");
            }

            replicationServerRoomsService ??= new();

            disconnectDialog.SetActive(false);
            bridge.onConnected.AddListener(_ => UpdateDialogsVisibility());
            bridge.onDisconnected.AddListener((_, _) => UpdateDialogsVisibility());
            bridge.onConnectionError.AddListener(OnConnectionError);

            if (!string.IsNullOrEmpty(RuntimeSettings.Instance.ProjectID))
            {
                cloudServiceReady = StartCoroutine(WaitForCloudService());
            }
            else if (regionDropdown.gameObject.activeInHierarchy)
            {
                noCloudPlaceholder.SetActive(true);
            }

            localToggleRefresher = StartCoroutine(LocalToggleRefresher());
        }

        private void OnDisable()
        {
            if (localToggleRefresher != null)
            {
                StopCoroutine(localToggleRefresher);
            }

            if (cloudServiceReady != null)
            {
                StopCoroutine(cloudServiceReady);
            }
        }

        private void Awake()
        {
            if (SimulatorUtility.IsSimulator)
            {
                gameObject.SetActive(false);
            }
        }

        private void Start()
        {
            lanOnlineToggle.onValueChanged.AddListener(OnToggleChanged);
            joinRoomButton.onClick.AddListener(() => JoinRoom(roomsListView.Selection.RoomData));
            showCreateRoomPanelButton.onClick.AddListener(ShowCreateRoomPanel);
            hideCreateRoomPanelButton.onClick.AddListener(HideCreateRoomPanel);
            createAndJoinRoomButton.onClick.AddListener(CreateRoomAndJoin);
            regionDropdown.onValueChanged.AddListener(OnRegionChanged);
            refreshRegionsButton.onClick.AddListener(RefreshRegions);
            refreshRoomsButton.onClick.AddListener(RefreshRooms);
            disconnectButton.onClick.AddListener(bridge.Disconnect);
            popupDismissButton.onClick.AddListener(HideError);

            popupDialog.SetActive(false);
            noRSPlaceholder.SetActive(false);
            noRoomsAvailable.SetActive(false);
            joinRoomButton.interactable = false;
            showCreateRoomPanelButton.interactable = false;
            refreshRegionsButton.interactable = false;
            templateRoomView.gameObject.SetActive(false);
            roomsListView = new ListView
            {
                Template = templateRoomView,
                onSelectionChange = view =>
                {
                    joinRoomButton.interactable = view != default && view.RoomData.UniqueId != default(RoomData).UniqueId;
                }
            };

            initialJoinRoomTitle = joinRoomTitleText.text;

            if (bridge.PlayerAccountAutoConnect is not CoherenceBridgePlayerAccount.AutoLoginAsGuest)
            {
                ConnectToCoherenceCloud();
            }
        }

        private void OnDestroy()
        {
            replicationServerRoomsService?.Dispose();
            playerAccount?.Dispose();
        }

        #endregion

        #region Cloud & Replication Server Requests
        private void ConnectToCoherenceCloud()
        {
            PlayerAccount.OnMainChanged += OnMainPlayerAccountChanged;
            OnMainPlayerAccountChanged(PlayerAccount.Main);
            CoherenceCloud.LoginAsGuest().OnFail(error =>
            {
                Debug.LogError($"Logging in to coherence Cloud Failed.\n{error}");

                windowContent.SetActive(false);
                errorText.text = "Logging in to coherence Cloud Failed.";
                errorText.gameObject.SetActive(true);
            });


            void OnMainPlayerAccountChanged(PlayerAccount mainPlayerAccount)
            {
                if (mainPlayerAccount is null)
                {
                    return;
                }

                playerAccount = mainPlayerAccount;
                PlayerAccount.OnMainChanged -= OnMainPlayerAccountChanged;
                if (bridge.PlayerAccountAutoConnect is CoherenceBridgePlayerAccount.None)
                {
                    bridge.CloudService = mainPlayerAccount.Services;
                }
            }
        }

        private IEnumerator WaitForCloudService()
        {
            ShowLoadingState();

            while (CloudRooms is not { IsLoggedIn : true })
            {
                yield return null;
            }

            HideLoadingState();

            RefreshRegions();
            cloudServiceReady = null;
        }

        private void RefreshRooms()
        {
            if (selectedRoomService == null)
            {
                return;
            }

            ShowLoadingState();
            noRoomsAvailable.SetActive(false);
            refreshRoomsButton.interactable = false;
            selectedRoomService.FetchRooms(OnFetchRooms);
        }

        private void CreateRoom()
        {
            var options = RoomCreationOptions.Default;
            options.KeyValues.Add(RoomData.RoomNameKey, roomNameInputField.text);
            options.MaxClients = RoomMaxPlayers;
            selectedRoomService?.CreateRoom(OnRoomCreated, options);
            HideCreateRoomPanel();
        }

        private void JoinRoom(RoomData roomData)
        {
            ShowLoadingState();
            bridge.JoinRoom(roomData);
        }

        private void CreateRoomAndJoin()
        {
            joinNextCreatedRoom = true;
            CreateRoom();
        }

        private void RefreshRegions()
        {
            ShowLoadingState();
            CloudRooms.RefreshRegions(OnRegionsChanged);
        }
        #endregion

        #region Local Replication Server
        private IEnumerator LocalToggleRefresher()
        {
            while (true)
            {
                var task = replicationServerRoomsService.IsOnline();
                yield return new WaitUntil(() => task.IsCompleted);

                var result = task.Result;

                HandleLocalServerStatus(result);

                yield return new WaitForSeconds(1f);
            }
        }

        private void HandleLocalServerStatus(bool result)
        {
            if (result && lanOnlineToggle.isOn && !noRSPlaceholder.activeInHierarchy)
            {
                return;
            }

            noRSPlaceholder.SetActive(lanOnlineToggle.isOn && !result);

            if (noRSPlaceholder.activeInHierarchy)
            {
                noRoomsAvailable.SetActive(false);
            }

            if (result && lanOnlineToggle.isOn)
            {
                selectedRoomService = replicationServerRoomsService;
                RefreshRooms();
            }
        }
        #endregion

        #region Request Callbacks
        private void OnRoomCreated(RequestResponse<RoomData> requestResponse)
        {
            if (requestResponse.Status != RequestStatus.Success)
            {
                joinNextCreatedRoom = false;

                var errorMessage = GetErrorFromResponse(requestResponse);
                ShowError("Error creating room", errorMessage);
                Debug.LogException(requestResponse.Exception);
                return;
            }

            var createdRoom = requestResponse.Result;
            if (joinNextCreatedRoom)
            {
                joinNextCreatedRoom = false;
                JoinRoom(createdRoom);
            }
            else
            {
                lastCreatedRoomUid = createdRoom.UniqueId;
                RefreshRooms();
            }
        }

        private void OnRegionsChanged(RequestResponse<IReadOnlyList<string>> requestResponse)
        {
            HideLoadingState();

            if (requestResponse.Status != RequestStatus.Success)
            {
                var errorMessage = GetErrorFromResponse(requestResponse);
                ShowError("Error refreshing regions", errorMessage);
                Debug.LogException(requestResponse.Exception);
                return;
            }

            var options = new List<Dropdown.OptionData>();

            var regions = requestResponse.Result;
            foreach (var region in regions)
            {
                options.Add(new Dropdown.OptionData(region));
            }

            regionDropdown.options = options;

            if (regions.Count > 0 && !lanOnlineToggle.isOn)
            {
                regionDropdown.captionText.text = regions[0];
                selectedRoomService = CloudRooms.GetRoomServiceForRegion(regions[0]);
                RefreshRooms();
            }
        }

        private void OnFetchRooms(RequestResponse<IReadOnlyList<RoomData>> requestResponse)
        {
            var rooms = requestResponse.Result;
            refreshRoomsButton.interactable = true;
            loadingSpinner.SetActive(false);
            HideLoadingState();

            joinRoomTitleText.text = initialJoinRoomTitle + " (0)";
            noRoomsAvailable.SetActive(requestResponse.Status != RequestStatus.Success || requestResponse.Result.Count == 0);

            if (requestResponse.Status != RequestStatus.Success)
            {
                roomsListView.Clear();

                var errorMessage = GetErrorFromResponse(requestResponse);
                ShowError("Error fetching rooms", errorMessage);
                Debug.LogException(requestResponse.Exception);
                return;
            }

            if (rooms.Count == 0)
            {
                roomsListView.Clear();
                return;
            }

            roomsListView.SetSource(rooms, lastCreatedRoomUid);
            lastCreatedRoomUid = default; // selection was already set.
            joinRoomTitleText.text = $"{initialJoinRoomTitle} ({rooms.Count})";

            joinRoomButton.interactable = roomsListView.Selection != default;
        }
        #endregion

        #region Error Handling
        private void ShowError(string title, string message = "Unknown Error")
        {
            popupDialog.SetActive(true);
            popupTitleText.text = title;
            popupText.text = message;
        }

        private void HideError()
        {
            popupDialog.SetActive(false);
        }

        private static string GetErrorFromResponse<T>(RequestResponse<T> requestResponse)
        {
            if (requestResponse.Exception is not RequestException requestException)
            {
                return default;
            }

            return requestException.ErrorCode switch
            {
                ErrorCode.InvalidCredentials => "Invalid authentication credentials, please login again.",
                ErrorCode.TooManyRequests => "Too many requests. Please try again in a moment.",
                ErrorCode.ProjectNotFound => "Project not found. Please check that the runtime key is properly setup.",
                ErrorCode.SchemaNotFound => "Schema not found. Please check if the schema currently used by the project matches the one used by the replication server.",
                ErrorCode.RSVersionNotFound => "Replication server version not found. Please check that the version of the replication server is valid.",
                ErrorCode.SimNotFound => "Simulator not found. Please check that the slug and the schema are valid and that the simulator has been uploaded.",
                ErrorCode.MultiSimNotListening => "The multi-room simulator used for this room is not listening on the required ports. Please check your multi-room sim setup.",
                ErrorCode.RoomsSimulatorsNotEnabled => "Simulator not enabled. Please make sure that simulators are enabled in the coherence Dashboard.",
                ErrorCode.RoomsSimulatorsNotUploaded => "Simulator not uploaded. You can use the coherence Hub to build and upload Simulators.",
                ErrorCode.RoomsVersionNotFound => "Version not found. Please make sure that client uses the correct 'sim-slug'.",
                ErrorCode.RoomsSchemaNotFound => "Schema not found. Please check if the schema currently used by the project matches the one used by the replication server.",
                ErrorCode.RoomsRegionNotFound => "Region not found. Please make sure that the selected region is enabled in the Dev Portal.",
                ErrorCode.RoomsInvalidTagOrKeyValueEntry => "Validation of tag and key/value entries failed. Please check if number and size of entries is within limits.",
                ErrorCode.RoomsCCULimit => "Room ccu limit for project exceeded.",
                ErrorCode.RoomsNotFound => "Room not found. Please refresh room list.",
                ErrorCode.RoomsInvalidSecret => "Invalid room secret. Please make sure that the secret matches the one received on room creation.",
                ErrorCode.RoomsInvalidMaxPlayers => "Room Max Players must be a value between 1 and the upper limit configured on the project dashboard.",
                ErrorCode.InvalidMatchMakingConfig => "Invalid matchmaking configuration. Please make sure that the matchmaking feature was properly configured in the Dev Portal.",
                ErrorCode.ClientPermission => "The client has been restricted from accessing this feature. Please check the game services settings on the Dev Portal.",
                ErrorCode.CreditLimit => "Monthly credit limit exceeded. Please check your organization credit usage in the Dev Portal.",
                ErrorCode.InDeployment => "One or more online resources are currently being provisioned. Please retry the request.",
                ErrorCode.FeatureDisabled => "Requested feature is disabled, make sure you enable it in the Game Services section of your coherence Dashboard.",
                ErrorCode.InvalidRoomLimit => "Room max players limit must be between 1 and 100.",
                ErrorCode.LobbyInvalidAttribute => "A specified Attribute is invalid.",
                ErrorCode.LobbyNameTooLong => "Lobby name must be shorter than 64 characters.",
                ErrorCode.LobbyTagTooLong => "Lobby tag must be shorter than 16 characters.",
                ErrorCode.LobbyNotFound => "Requested Lobby wasn't found.",
                ErrorCode.LobbyAttributeSizeLimit => "A specified Attribute has surpassed the allowed limits. Lobby limit: 2048. Player limit: 256. Attribute size is calculated off key length + value length of all attributes combined.",
                ErrorCode.LobbyNameAlreadyExists => "A lobby with this name already exists.",
                ErrorCode.LobbyRegionNotFound => "Specified region for this Lobby wasn't found.",
                ErrorCode.LobbyInvalidSecret => "Invalid secret specified for lobby.",
                ErrorCode.LobbyFull => "This lobby is currently full.",
                ErrorCode.LobbyActionNotAllowed => "You're not allowed to perform this action on the lobby.",
                ErrorCode.LobbyInvalidFilter => "The provided filter is invalid. You can use Filter.ToString to debug the built filter you're sending.",
                ErrorCode.LobbyNotCompatible => "Schema not found. Please check if the schema currently used by the project matches the one used by the replication server.",
                ErrorCode.LobbySimulatorNotEnabled => "Simulator not enabled. Please make sure that simulators are enabled in the coherence Dashboard.",
                ErrorCode.LobbySimulatorNotUploaded => "Simulator not uploaded. You can use the coherence Hub to build and upload Simulators.",
                ErrorCode.LobbyLimit => "You cannot join more than three lobbies simultaneously.",
                ErrorCode.LoginInvalidUsername => "Username given is invalid. Only alphanumeric, dashes and underscore characters are allowed. It must start with a letter and end with a letter/number. No double dash/underscore characters are allowed (-- or __).",
                ErrorCode.LoginInvalidPassword => "Password given is invalid. Password cannot be empty.",
                ErrorCode.RestrictedModeCapReached => "Total user capacity for restricted mode server reached.",
                ErrorCode.LoginDisabled => "This authentication method is disabled.",
                ErrorCode.LoginInvalidApp => "The provided App ID is invalid.",
                ErrorCode.LoginNotFound => "No player account has been linked to the authentication method that was used.",
                ErrorCode.OneTimeCodeExpired => "The one-time code has already expired.",
                ErrorCode.OneTimeCodeNotFound => "The one-time code was not found.",
                ErrorCode.IdentityLimit => "Unique identity limit reached.",
                ErrorCode.IdentityNotFound => "Identity not found.",
                ErrorCode.IdentityRemoval => "Tried to unlink last authentication method from player account.",
                ErrorCode.IdentityTaken => "Identity already linked to another player account.",
                ErrorCode.IdentityTotalLimit => "Maximum allowed identity limit reached.",
                ErrorCode.InvalidConfig => "Invalid configuration. Please make sure that all the necessary information has been provided in coherence Dashboard.",
                ErrorCode.InvalidInput => "Invalid input. Please make sure to provide all required arguments.",
                ErrorCode.PasswordNotSet => "Password has not been set for the player account.",
                ErrorCode.UsernameNotAvailable => "The username is already taken by another player account.",
                _ => requestException.Message,
            };
        }

        private void OnConnectionError(CoherenceBridge bridge, ConnectionException exception)
        {
            HideLoadingState();
            RefreshRooms();

            var (title, message) = exception.GetPrettyMessage();

            Debug.LogError(message);
            ShowError(title, message);
        }
        #endregion

        #region Update UI
        private void OnToggleChanged(bool isLanToggled)
        {
            regionDropdown.interactable = !isLanToggled;
            regionSection.SetActive(!isLanToggled);
            noRSPlaceholder.SetActive(isLanToggled);
            noRoomsAvailable.SetActive(false);
            noCloudPlaceholder.SetActive(!isLanToggled && !CloudRooms.IsLoggedIn);
            HideCreateRoomPanel();
            loadingSpinner.SetActive(false);

            cloudText.font = isLanToggled ? normalFont : boldFont;
            lanText.font = isLanToggled ? boldFont : normalFont;

            selectedRoomService = null;

            if (isLanToggled)
            {
                RefreshLocalToggle();
            }
            else if (CloudRooms.IsLoggedIn)
            {
                var currentRegion = regionDropdown.options[regionDropdown.value].text;
                selectedRoomService = CloudRooms.GetRoomServiceForRegion(currentRegion);
                RefreshRooms();
            }
        }
        private void ShowCreateRoomPanel()
        {
            createRoomPanel.SetActive(true);
        }

        private void HideCreateRoomPanel()
        {
            createRoomPanel.SetActive(false);
        }

        private async void RefreshLocalToggle()
        {
            var result = await replicationServerRoomsService.IsOnline();

            HandleLocalServerStatus(result);
        }

        private void UpdateDialogsVisibility()
        {
            connectDialog.SetActive(!bridge.IsConnected);
            disconnectDialog.SetActive(bridge.IsConnected);

            if (!bridge.IsConnected)
            {
                RefreshRooms();
            }
        }

        private void HideLoadingState()
        {
            loadingSpinner.SetActive(false);
            showCreateRoomPanelButton.interactable = true;
            refreshRegionsButton.interactable = true;
            joinRoomButton.interactable = roomsListView != null && roomsListView.Selection != default
                                                                && roomsListView.Selection.RoomData.UniqueId != default(RoomData).UniqueId;
        }

        private void ShowLoadingState()
        {
            loadingSpinner.SetActive(true);
            showCreateRoomPanelButton.interactable = false;
            refreshRegionsButton.interactable = false;
            joinRoomButton.interactable = false;
        }

        private void OnRegionChanged(int region)
        {
            if (!CloudRooms.IsLoggedIn)
            {
                return;
            }

            var regionText = regionDropdown.options[region].text;

            selectedRoomService = CloudRooms.GetRoomServiceForRegion(regionText);
            RefreshRooms();
        }
        #endregion
    }

    internal class ListView
    {
        public ConnectDialogRoomView Template;
        public Action<ConnectDialogRoomView> onSelectionChange;

        public ConnectDialogRoomView Selection
        {
            get => selection;
            set
            {
                if (selection != value)
                {
                    selection = value;
                    lastSelectedId = selection == default ? default : selection.RoomData.UniqueId;
                    onSelectionChange?.Invoke(Selection);
                    foreach (var viewRow in Views)
                    {
                        viewRow.IsSelected = selection == viewRow;
                    }
                }
            }
        }

        public List<ConnectDialogRoomView> Views { get; }
        private ConnectDialogRoomView selection;
        private HashSet<ulong> displayedIds = new();
        private ulong lastSelectedId;

        public ListView(int capacity = 50)
        {
            Views = new List<ConnectDialogRoomView>(capacity);
        }

        public void SetSource(IReadOnlyList<RoomData> dataSource, ulong idToSelect = default)
        {
            if (dataSource.Count == Views.Count && dataSource.All(s => displayedIds.Contains(s.UniqueId)))
            {
                return;
            }
            displayedIds = new HashSet<ulong>(dataSource.Select(d => d.UniqueId));

            Clear();

            if (dataSource.Count <= 0)
            {
                return;
            }

            var sortedData = dataSource.ToList();
            sortedData.Sort((roomA, roomB) =>
            {
                var strCompare = String.CompareOrdinal(roomA.RoomName, roomB.RoomName);
                if (strCompare != 0)
                {
                    return strCompare;
                }

                return (int)(roomA.UniqueId - roomB.UniqueId);
            });

            if (idToSelect == default && lastSelectedId != default)
            {
                idToSelect = lastSelectedId;
            }

            foreach (var data in sortedData)
            {
                var view = MakeViewItem(data);
                Views.Add(view);
                if (data.UniqueId == idToSelect)
                {
                    Selection = view;
                }
            }
        }

        private ConnectDialogRoomView MakeViewItem(RoomData data, bool isSelected = false)
        {
            var view = Object.Instantiate(Template, Template.transform.parent);
            view.RoomData = data;
            view.IsSelected = isSelected;
            view.OnClick = () => Selection = view;
            view.gameObject.SetActive(true);
            return view;
        }

        public void Clear()
        {
            Selection = default;
            foreach (var view in Views)
            {
                Object.Destroy(view.gameObject);
            }
            Views.Clear();
        }
    }
}
