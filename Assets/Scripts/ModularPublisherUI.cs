using UnityEngine;
using UnityEngine.UI;
using UnityOpcUaPublisher;
using System.Collections.Generic;

// Serializovatelny wrapper pre PublisherField z DLL kniznice
[System.Serializable]
public class InspectorFieldConfig
{
    public string Name;
    [Tooltip("Typy: String, Float, Int32, Boolean, DateTime...")]
    public string TypeName;
}

public class ModularPublisherUI : MonoBehaviour
{
    [Header("UI References")]
    public InputField BrokerInput;
    public InputField TopicInput;
    public Button ConnectButton;
    public Text ConnectButtonText;

    // Zoznam premenn˝ch ktorÈ je moûnÈ spolu s d·tov˝m typom urËiù priamo prostrednÌctvom Unity editora
    [Header("Data Schema")]
    public List<InspectorFieldConfig> dataSchema = new List<InspectorFieldConfig>();

    [Header("Data Controls")]
    public InputField MessageInput;
    public Button SendMessageButton;
    public Slider ValueSlider;
    public Text SliderValueText;
    public Toggle BoolToggle; 

    // Inötancia DLL
    private OpcUaPublisher _publisher;
    private bool _isConnected = false;

    // N·zvy premenn˝ch, ktorÈ budeme posielaù (Hardcoded pre testovanie)
    private const string DEMO_FIELD_MSG = "MyTextMessage";
    private const string DEMO_FIELD_VAL = "MySliderValue";
    private const string DEMO_FIELD_BOOL = "MySwitch";

    void Start()
    {
        _publisher = new OpcUaPublisher();

        // PrednastavenÈ hodnoty
        BrokerInput.text = "mqtt://localhost:1883";
        TopicInput.text = "Unity/ModularTest";

        if (dataSchema.Count == 0) 
        {   
            // Naplenenie zoznamu premenn˝ch s prÌsluön˝m d·tov˝m typom v prÌpade, ûe cez Unity editor neboli pridanÈ ûiadne premennÈ (pre testovacie ˙Ëely)
            dataSchema.Add(new InspectorFieldConfig { Name = DEMO_FIELD_MSG, TypeName = "String" });
            dataSchema.Add(new InspectorFieldConfig { Name = DEMO_FIELD_VAL, TypeName = "Float" });
            dataSchema.Add(new InspectorFieldConfig { Name = DEMO_FIELD_BOOL, TypeName = "Boolean" });
        }

        // Listeners
        ConnectButton.onClick.AddListener(ToggleConnection);
        SendMessageButton.onClick.AddListener(SendTextMessage);
        ValueSlider.onValueChanged.AddListener(OnSliderChanged);


        // Inicializ·cia UI
        UpdateUIState();
    }

    void ToggleConnection()
    {
        if (_isConnected)
        {
            // Odpojenie
            _publisher.Stop();
            _isConnected = false;
            Debug.Log("Publisher zastaven˝.");
        }
        else
        {
            // --- 1. ätart s konfigur·ciou z UI ---
            string broker = BrokerInput.text;
            string topic = TopicInput.text;

            // Preklopenie premenn˝ch s d·tov˝mi typmi InspectorFieldConfig z Unity editora do PublisherField definovan˝ch v DLL kniûnici
            List<PublisherField> dllFields = new List<PublisherField>();
            foreach (var item in dataSchema)
            {
                // VytvorÌme objekt pre DLL
                dllFields.Add(new PublisherField(item.Name, item.TypeName));
            }

            try
            {
                _publisher.Start(broker, topic, dllFields);
                _isConnected = true;
                Debug.Log($"PripojenÈ na {broker} | Topic: {topic}");

                // Inicializacia nul v jednotlivych odosielanych datovych poliach, aby sa nepublishovali chyby 0x000000 (neinicializovane premenne)
                _publisher.PublishValue(DEMO_FIELD_MSG, "Ready");
                _publisher.PublishValue(DEMO_FIELD_VAL, 0.0f);
                _publisher.PublishValue(DEMO_FIELD_BOOL, BoolToggle.isOn);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Chyba pripojenia: {ex.Message}");
            }
        }
        UpdateUIState();
    }

    // Odoslanie textu (String) na kliknutie
    void SendTextMessage()
    {
        if (_isConnected)
        {
            string msg = MessageInput.text;
            _publisher.PublishValue(DEMO_FIELD_MSG, msg);
            Debug.Log($"OdoslanÈ: {msg}");
        }
    }

    // Odosielanie ËÌsla (Float) pri posune slidera
    void OnSliderChanged(float val)
    {
        if (SliderValueText) SliderValueText.text = val.ToString("F2");

        if (_isConnected)
        {
            _publisher.PublishValue(DEMO_FIELD_VAL, val);
        }
    }

    // Odosielanie Bool v Update (napr. kaûd˙ sekundu alebo pri zmene)
    public void SendBoolValue(bool value)
    {
        if (_isConnected)
        {
            _publisher.PublishValue(DEMO_FIELD_BOOL, value);
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
        if (_publisher != null) _publisher.Stop();
    }
}
