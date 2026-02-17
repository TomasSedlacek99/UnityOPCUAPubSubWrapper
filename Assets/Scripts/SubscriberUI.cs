using UnityEngine;
using UnityEngine.UI;
using UnityOpcUaPublisher; // Namespace tvojej DLL
using System.Collections.Generic;
using System.Collections.Concurrent; // Pre bezpeËnÈ fronty vl·kien

public class SubscriberUI : MonoBehaviour
{
    [Header("MQTT Settings")]
    public InputField BrokerInput;
    public InputField TopicInput;
    public Button ConnectButton;
    public Text ConnectButtonText;

    [Tooltip("MusÌ sa zhodovaù s PublisherId v odosielateæovi! Default v naöej DLL je 'UnityPublisher'")]
    public string TargetPublisherId = "UnityPublisher";

    [Header("UI References")]
    public Text MessageText;   // Sem prijat˝ text
    public Text ValueText;     // Sem prijatÈ ËÌslo
    public Toggle BoolDisplay; // Sem prijat˝ prepÌnaË

    // Inötancia z DLL
    private OpcUaSubscriber _subscriber;
    private bool _isConnected = false;

    // Fronta na prenos d·t z pozadia do hlavnÈho vl·kna Unity
    private ConcurrentQueue<(string, object)> _mainThreadQueue = new ConcurrentQueue<(string, object)>();

    void Start()
    {
        _subscriber = new OpcUaSubscriber();

        // PrednastavenÈ hodnoty
        BrokerInput.text = "mqtt://localhost:1883";
        TopicInput.text = "Unity/ModularTest";

        ConnectButton.onClick.AddListener(ToggleConnection);
        UpdateUIState();
    }

    void ToggleConnection() 
    {
        if (_isConnected)
        {
            // Odpojenie
            _subscriber.Stop();
            _isConnected = false;
            Debug.Log("Subscriber zastaven˝.");
        }
        else 
        {
            // Zoznam premenn˝ch, ktorÈ chceme ËÌtaù (musia sedieù s t˝m, Ëo posiela Publisher)
            List<string> varsToRead = new List<string> { "MyTextMessage", "MySliderValue", "MySwitch" };

            try
            {
                // Prihl·senie sa na event (callback beûÌ na pozadÌ)
                _subscriber.OnMessageReceived += (name, val) =>
                {
                    // Len vloûÌme do fronty, spracujeme v Update()
                    _mainThreadQueue.Enqueue((name, val));
                };

                _isConnected = true;
                // Spustenie Subscribera
                _subscriber.Subscribe(BrokerInput.text, TopicInput.text, TargetPublisherId, varsToRead);
            }
            catch (System.Exception ex)
            {
                Debug.LogError(ex);
            }
        }
        UpdateUIState();
    }

    void Update()
    {
        // Vyberanie d·t z fronty na hlavnom vl·kne
        while (_mainThreadQueue.TryDequeue(out var data))
        {
            string varName = data.Item1;
            object value = data.Item2;

            ProcessData(varName, value);
        }
    }

    // Tu je moûnÈ implementovaù vlastn˙ logiku pre Subscribera, staËÌ vloûiù vlastn˝ "Case" s prÌsluön˝m n·zvom odosielanej premennej na danom topicu
    // a s logikou spracovania tejto spr·vy
    void ProcessData(string name, object value)
    {
        Debug.Log($"PrijatÈ: {name} = {value}");

        switch (name)
        {
            case "MyTextMessage":
                if (MessageText) MessageText.text = value.ToString();
                break;

            case "MySliderValue":
                if (ValueText) ValueText.text = value.ToString();
                break;

            case "MySwitch":
                // JSON niekedy poöle true/false, niekedy 0/1, sk˙sime to parsovaù
                if (BoolDisplay)
                {
                    if (bool.TryParse(value.ToString(), out bool result))
                        BoolDisplay.isOn = result;
                    else if (int.TryParse(value.ToString(), out int intResult))
                        BoolDisplay.isOn = intResult > 0;
                }
                break;
        }
    }

    void UpdateUIState()
    {
        if (ConnectButtonText)
        {
            ConnectButtonText.text = _isConnected ? "Disconnect" : "Connect & Start";
            ConnectButton.image.color = _isConnected ? Color.red : Color.green;
        }

        // Zablokovanie vstupov keÔ sme pripojenÌ
        BrokerInput.interactable = !_isConnected;
        TopicInput.interactable = !_isConnected;
    }

    void OnDisable()
    {
        if (_subscriber != null) _subscriber.Stop();
    }
}