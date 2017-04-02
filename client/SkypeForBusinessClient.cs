using LyncSDKExtensions;
using Microsoft.Lync.Model;
using Microsoft.Lync.Model.Conversation;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace SkypeForBusiness
{ 
    public class Client : IDisposable
    {

        private LyncClient _lyncClient;
        private bool _thisProcessInitializedLync;
        public bool ThisProcessInitializedLync
        {
            get { return _thisProcessInitializedLync; }
        }

        private bool _badSignInAddressIdentified;
        public bool BadSignInAddressIdentified
        {
            get { return _badSignInAddressIdentified; }
        }

        public bool SignInAborted
        {
            get { return Retries >= 10 || _badSignInAddressIdentified; }
        }

        private bool _timeoutOccured;
        public bool TimeoutOccured
        {
            get { return _timeoutOccured; }
            private set { _timeoutOccured = true; }
        }

        private bool _lyncApInitializationFailed;
        public bool LyncApInitializationFailed
        {

            get { return _lyncApInitializationFailed; }
        }

        private string _lyncApInitializationErrorMessage;
        public string LyncApInitializationErrorMessage
        {
            get { return _lyncApInitializationErrorMessage; }
        }

        private int Retries
        {
            get { return _retries; }
            set { _retries = value; }
        }

        private static Mutex _lock = new Mutex();
        private string _signInAddress;
        private string _username;
        private string _password;

        private int _retries;
        private EventWaitHandle _signInProcedureEvent;
        private EventWaitHandle _signOutProcedureEvent;
        private EventWaitHandle _signShutdownProcedureEvent;

        public Tuple<StatusValue, string> SignInStatus
        {
            get
            {
                if (TimeoutOccured)
                    return Tuple.Create(StatusValue.Down, "Timeout occured during SignIn procedure.");

                if (LyncApInitializationFailed)
                    return Tuple.Create(StatusValue.Down, LyncApInitializationErrorMessage);

                if (SignInAborted)
                {
                    var status = StatusValue.Down;

                    if (BadSignInAddressIdentified)
                        return Tuple.Create(status, $"SignIn action has been aborted. SignIn address {_signInAddress} looks invalid.");
                    else
                        return Tuple.Create(status, "SignIn action has been aborted. Please verify your credentials parameters.");
                }

                return Tuple.Create(StatusValue.Up, string.Empty);
            }
        }



        public bool IsClientValid
        {
            get
            {
                return _lyncClient != null
                  && _lyncClient.State != ClientState.Invalid
                  && _lyncClient.State != ClientState.SigningOut;
            }
        }


        public Client()
        {
            _signInAddress = string.Empty;
            _username = string.Empty;
            _password = string.Empty;
            _retries = 0;
            _timeoutOccured = false;
            _badSignInAddressIdentified = false;

            _signInProcedureEvent = new AutoResetEvent(false);
            _signOutProcedureEvent = new AutoResetEvent(false);
            _signShutdownProcedureEvent = new AutoResetEvent(false);

            try
            {
                _lock.WaitOne();
                GetValidLyncClient();

                if (IsClientValid)
                {
                    _lyncClient.StateChanged += _LyncClient_StateChanged;
                    _lyncClient.SignInDelayed += _LyncClient_SignInDelayed;
                    _lyncClient.CredentialRequested += _LyncClient_CredentialRequested;
                }
                else
                {
                    _lyncClient = null;
                    _lyncApInitializationFailed = true;
                    _lyncApInitializationErrorMessage = "Client in invalid state";
                }

            }
            catch (ClientNotFoundException ex)
            {
                _lyncClient = null;
                _lyncApInitializationFailed = true;
                _lyncApInitializationErrorMessage = $"Client not found: {ex.Message}";
            }
            catch (NotStartedByUserException ex)
            {
                _lyncClient = null;
                _lyncApInitializationFailed = true;
                _lyncApInitializationErrorMessage = $"Client not started by user: {ex.Message}";
            }
            catch (Exception ex)
            {
                _lyncClient = null;
                _lyncApInitializationFailed = true;
                _lyncApInitializationErrorMessage = $"Exception occured during lync sdk initialization: {ex.Message}";
            }
        }

        private void GetValidLyncClient()
        {
            int retryToGetValidClient = 1;
            while (retryToGetValidClient <= 3 && !IsClientValid)
            {
                _lyncClient = LyncClient.GetClient();

                if (!IsClientValid)
                {
                    Trace.Write("Attempt {retryToGetValidClient}/3 to get a valid Lync Client Reference");

                    if (_lyncClient.State == ClientState.SigningOut)
                    {
                        KillLyncProcess();
                    }


                    retryToGetValidClient += 1;
                    Thread.Sleep(250);
                }
            }
        }

        private void KillLyncProcess()
        {
            var lyncProcesses = Process.GetProcessesByName("lync");
            foreach (var lyncprocess in lyncProcesses)
            {
                lyncprocess.Kill();
            }
        }

        public void SignIn(string signInAddress, string username, string password)
        {
            _signInAddress = signInAddress;
            _username = username;
            _password = password;

            if ((_lyncClient == null))
            {
                return;
            }

            try
            {
                //2-4) Client state of uninitialized means that Lync Is configured for UI suppression mode And
                //must be initialized before a user can sign in to Lync

                if (_lyncClient.State == ClientState.Uninitialized)
                {
                    _lyncClient.BeginInitialize(ar =>
                    {
                        _lyncClient.EndInitialize(ar);
                        _thisProcessInitializedLync = true;
                    }, null);

                    //If the Lync client Is signed out, sign into the Lync client
                }
                else if ((_lyncClient.State == ClientState.SignedOut))
                {
                    SignUserIn();

                    // A sign in operation Is pending
                }
                else if ((_lyncClient.State == ClientState.SigningIn) || _lyncClient.State == ClientState.SignedIn)
                {
                    SignOut();
                    SignUserIn();
                }

                if ((!_signInProcedureEvent.WaitOne(75000)))
                {
                    //signIn timeout occured
                    TimeoutOccured = true;
                }
            }
            catch (Exception ex)
            {
                Debug.Assert(false, ex.Message);
            }
        }

        private void _LyncClient_StateChanged(object sender, ClientStateChangedEventArgs e)
        {
            switch (e.NewState)
            {
                case ClientState.SignedOut:
                    if (e.OldState == ClientState.Initializing)
                        SignUserIn();

                    if (e.OldState == ClientState.SigningOut && _lyncClient.InSuppressedMode == true)
                        _lyncClient.SignInConfiguration.ForgetMe(_signInAddress);

                    break;
                case ClientState.Uninitialized:
                    if (e.OldState == ClientState.ShuttingDown)
                        _lyncClient.StateChanged -= _LyncClient_StateChanged;

                    break;
            }
        }

        private void _LyncClient_CredentialRequested(object sender, CredentialRequestedEventArgs e)
        {
            //If the request for credentials comes from Lync server then sign out, get new creentials
            //and sign in.
            if (e.Type == CredentialRequestedType.LyncAutodiscover)
            {
                try
                {
                    _retries = _retries + 1;
                    if ((_retries < 10))
                    {
                        _lyncClient.BeginSignOut(ar =>
                        {
                            _lyncClient.EndSignOut(ar);
                            //Ask user for credentials and attempt to sign in again
                            SignUserIn();
                        }, null);
                    }
                    else
                    {
                        _lyncClient.BeginSignOut(ar =>
                        {
                            _lyncClient.EndSignOut(ar);
                            _signInProcedureEvent.Set();
                        }, null);
                    }
                }
                catch (Exception ex)
                {
                    Trace.WriteLine($"Exception on attempt to sign in, abandoning sign in: {ex.Message}, Lync Client sign in delay");
                }
            }
            else
            {
                e.Submit(_username, _password, false);
            }
        }



        public void SignUserIn()
        {
            try
            {
                _lyncClient.BeginSignIn(_signInAddress, _username, _password, ar =>
                {
                    try
                    {
                        _lyncClient.EndSignIn(ar);
                        _signInProcedureEvent.Set();
                    }
                    catch (LyncClientException clientExcpetion)
                    {
                        string message = clientExcpetion.Message;
                        if (message.StartsWith("Generic COM Exception", StringComparison.InvariantCultureIgnoreCase))
                        {
                            _badSignInAddressIdentified = true;
                            _signInProcedureEvent.Set();
                        }
                    }
                    catch (Exception exc)
                    {
                        Trace.WriteLine($"exception on endsignin: {exc.Message})");
                    }

                }, null);
            }
            catch (ArgumentException ae)
            {
                Trace.WriteLine($"exception on beginsignin: {ae.Message})");
            }
        }

        public void SignOut()
        {
            if (_lyncClient == null)
                return;

            try
            {
                if (_lyncClient.State == ClientState.SignedIn || _lyncClient.State == ClientState.SigningIn)
                {
                    _lyncClient.BeginSignOut(ar =>
                    {
                        try
                        {
                            _lyncClient.EndSignOut(ar);
                        }
                        catch (Exception exc)
                        {
                            Trace.WriteLine($"exception on endsignin: {exc.Message})");
                        }
                        finally
                        {
                            _signOutProcedureEvent.Set();
                        }

                    }, null);

                    _signOutProcedureEvent.WaitOne();
                }


                if ((_lyncClient.State == ClientState.SignedOut))
                {
                    _lyncClient.SignInConfiguration.ForgetMe(_signInAddress);
                }
            }
            catch (ArgumentException ae)
            {
                Trace.WriteLine($"exception on beginsignin: {ae.Message})");
            }
        }

        private void _LyncClient_SignInDelayed(object sender, SignInDelayedEventArgs e)
        {
            try
            {
                _lyncClient.BeginSignOut(ar => { _lyncClient.EndSignOut(ar); }, null);
            }
            catch (LyncClientException lce)
            {
                Trace.WriteLine($"Exception on sign out in SignInDelayed event: {lce.Message}");
            }
        }

        public void RemoveHandlers()
        {
            if (_lyncClient == null)
            {
                return;
            }

            _signInProcedureEvent.Reset();
            _signOutProcedureEvent.Reset();
            _signShutdownProcedureEvent.Reset();

            _lyncClient.StateChanged -= _LyncClient_StateChanged;
            _lyncClient.SignInDelayed -= _LyncClient_SignInDelayed;
            _lyncClient.CredentialRequested -= _LyncClient_CredentialRequested;
        }

        private List<string> _recipients;
        private string _message;
        private AutoResetEvent _sendIMEvent;
        private AutoResetEvent _allRecipientsAdded;
        private List<string> _unauthorizedRecipients;
        private List<string> _offlineParticipants;

        private int _addedParticipants;
        public bool AreRecipientsValid
        {
            get { return _unauthorizedRecipients.Count == 0; }
        }

        public Tuple<StatusValue, string> SendIntantMessageStatus
        {
            get
            {
                if (SendIMTimeout)
                {
                    return Tuple.Create(StatusValue.Down, "Timeout occured during Send Instant Message procedure.");
                }

                if (SendIMTimeoutParticipantsAdded)
                {
                    return Tuple.Create(StatusValue.Down, "Timeout occured while adding participants to the conversation.");
                }

                if (!ConversationCanBeBuilt)
                {
                    return Tuple.Create(StatusValue.Down, "Conversation cannot be initiated. " + "All participants cannot be added to the conversation :" + $"{String.Join(", ", _unauthorizedRecipients)}");
                }

                if (AllPArticipantsOffline)
                {
                    return Tuple.Create(StatusValue.Down, "No users involved in the conversation received the message.");
                }

                return Tuple.Create(StatusValue.Up, string.Empty);
            }
        }

        private bool SendIMTimeout { get; set; }
        private bool SendIMTimeoutParticipantsAdded { get; set; }
        private bool ConversationCanBeBuilt { get; set; }
        private bool AllPArticipantsOffline { get; set; }


        private void RemoveConversationHandler(Conversation currentConversation)
        {
            _sendIMEvent.Reset();
            _allRecipientsAdded.Reset();

            if (currentConversation != null)
            {
                currentConversation.ParticipantAdded -= Conversation_ParticipantAdded;
            }
            _lyncClient.ConversationManager.ConversationAdded -= ConversationManager_ConversationAdded;
        }

        private void SendMessageCallback(InstantMessageModality imModality, IAsyncResult asyncResult)
        {
            try
            {
                imModality.EndSendMessage(asyncResult);
            }
            catch (Exception ex)
            {
                Trace.WriteLine("Exception during SendMessageCallback");
                Trace.WriteLine($"{ex.Message}");
                Trace.WriteLine($"{ex.StackTrace}");
            }
            finally
            {
                _sendIMEvent.Set();
            }
        }

        private void OnAllRecipientsAdded(Conversation currentConversation)
        {
            InstantMessageModality imModality = currentConversation.Modalities[ModalityTypes.InstantMessage] as InstantMessageModality;

            IDictionary<InstantMessageContentType, string> textMessage = new Dictionary<InstantMessageContentType, string>();
            textMessage.Add(InstantMessageContentType.PlainText, _message);

            if (imModality.CanInvoke(ModalityAction.SendInstantMessage))
            {
                IAsyncResult asyncResult = imModality.BeginSendMessage(textMessage, ar => { SendMessageCallback(imModality, ar); }, null);
                if (!_sendIMEvent.WaitOne(75000))
                {
                    Trace.WriteLine($"SendIM action failed in timeout recipients = {string.Join(", ", _recipients)}");
                    SendIMTimeout = true;
                }
                else
                {
                    //if at the end of the send, it remains only 1 participant (me) ==> All other participants are offline
                    //AllPArticipantsOffline = (currentConversation.Participants.Count = 1)

                    Trace.WriteLine($"SendIM action succedeed recipients = {string.Join(", ", _recipients)}");
                    Thread.Sleep(250);
                }
            }
        }

        public void SendMessageTo(string message, IList<string> recipients)
        {
            Conversation _conversation = null;
            try
            {
                var conversationManager = _lyncClient.ConversationManager;

                _addedParticipants = 0;
                SendIMTimeout = false;
                SendIMTimeoutParticipantsAdded = false;
                ConversationCanBeBuilt = true;
                AllPArticipantsOffline = false;

                _sendIMEvent = new AutoResetEvent(false);
                _allRecipientsAdded = new AutoResetEvent(false);
                _unauthorizedRecipients = new List<string>();
                _offlineParticipants = new List<string>();

                _recipients = new List<string>();
                _recipients.Clear();

                _recipients.AddRange(recipients);

                _message = message;

                conversationManager.ConversationAdded += ConversationManager_ConversationAdded;
                _conversation = conversationManager.AddConversation();


                if (_allRecipientsAdded.WaitOne(75000))
                {
                    if (!AllPArticipantsOffline && ConversationCanBeBuilt)
                    {
                        OnAllRecipientsAdded(_conversation);
                    }

                }
                else
                {
                    SendIMTimeoutParticipantsAdded = true;
                }
            }
            finally
            {
                RemoveConversationHandler(_conversation);
            }
        }


        private void ConversationManager_ConversationAdded(object sender, ConversationManagerEventArgs e)
        {
            _unauthorizedRecipients.Clear();

            e.Conversation.ParticipantAdded += Conversation_ParticipantAdded;

            foreach (var recipient in _recipients)
            {
                try
                {
                    e.Conversation.AddParticipant(_lyncClient.ContactManager.GetContactByUri($"sip:{recipient}"));
                }
                catch (Exception ex)
                {
                    Trace.WriteLine(ex.Message);
                    Trace.WriteLine(ex.StackTrace);
                    _unauthorizedRecipients.Add("{recipient}");
                    Trace.WriteLine($"{recipient} cannot be added to the conversation");

                    ConversationCanBeBuilt = false;
                }
            }
        }



        private void Conversation_ParticipantAdded(object sender, ParticipantCollectionChangedEventArgs e)
        {
            var currentParticipant = e.Participant;
            //add event handlers for modalities of participants other than self participant
            if (currentParticipant.IsSelf == false)
            {
                var retries = 5;

                var participantAvailibility = ContactAvailability.None;
                while (retries > 0 && !currentParticipant.IsAvailable())
                {
                    participantAvailibility = (ContactAvailability)currentParticipant.Contact.GetContactInformation(ContactInformationType.Availability);

                    Trace.WriteLine($"Trying to add {currentParticipant.Contact.Uri} to the discussion, availibility={participantAvailibility}");
                    if (!currentParticipant.IsAvailable())
                    {
                        retries = retries - 1;
                        Thread.Sleep(1500);
                    }
                }

                if (!currentParticipant.IsAvailable())
                {
                    _offlineParticipants.Add(currentParticipant.Contact.Uri);
                }

                if (_offlineParticipants.Count == _recipients.Count)
                {
                    AllPArticipantsOffline = true;
                }

                _addedParticipants = _addedParticipants + 1;
                if (_addedParticipants == _recipients.Count || !ConversationCanBeBuilt)
                {
                    _allRecipientsAdded.Set();
                }
            }
        }

        public void Dispose()
        {
            try
            {
                _lock.ReleaseMutex();
            }
            finally { }
        }
    }
}
