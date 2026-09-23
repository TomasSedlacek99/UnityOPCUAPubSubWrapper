# 🏭 Unity OPC UA PubSub Client (MQTT & JSON)

Tento repozitár obsahuje kompletné riešenie pre integráciu priemyselného štandardu **OPC UA PubSub** (Part 14) do herného enginu **Unity**.

Jadrom projektu je vlastná, vysoko optimalizovaná knižnica `UnityOpcUaPublisher.dll`, ktorá abstrahuje komplexitu OPC UA stacku do jednoduchého API pre C# vývojárov. Riešenie využíva transportný protokol **MQTT** a **JSON** kódovanie dát, čo zaručuje kompatibilitu s modernými IoT zariadeniami a cloudovými službami.

---

## 📚 Hĺbková technická špecifikácia DLL

Knižnica `UnityOpcUaPublisher.dll` bola vyvinutá s cieľom preklenúť priepasť medzi .NET Standard svetom a špecifickými požiadavkami Unity enginu.

### 1. Architektúra a Štandardy

Knižnica implementuje špecifikáciu **OPC UA Part 14: PubSub**. Na rozdiel od klasického Client-Server modelu, PubSub umožňuje efektívny, asynchrónny prenos dát "One-to-Many".

* **Target Framework:** `.NET Standard 2.1` (kompatibilné s Unity 2021.3+).
* **Transport Profile:** `http://opcfoundation.org/UA-Profile/Transport/pubsub-mqtt-json`.
* **Message Encoding:** JSON (podľa OPC UA Part 6).
* **Serializácia:** Využíva `Newtonsoft.Json` pre rýchlu serializáciu a deserializáciu dátových štruktúr.

### 2. Interná implementácia

Knižnica interne spravuje životný cyklus OPC UA aplikácie (`UaPubSubApplication`), čím odbremeňuje vývojára od manuálnej konfigurácie `WriterGroups` a `DataSetWriters`.

#### **Publisher (Odosielateľ)**

Trieda `OpcUaPublisher` dynamicky generuje konfiguráciu publikovania na základe vstupných dát.

* **Dynamické mapovanie typov:** Knižnica obsahuje interný prevodník (Type Marshaler), ktorý transformuje C# primitívne typy (napr. `float`, `int`, `string`) na OPC UA `Variant` typy, resp. `JsonWriterGroupMessageDataType`.
* **Network Message Construction:** Pri volaní `PublishValue` sa nevytvára nové spojenie. Knižnica udržiava otvorené MQTT spojenie a iba aktualizuje payload v `NetworkMessage`.
* **Podporované typy:** `Boolean`, `SByte`, `Int16/32/64`, `UInt16/32/64`, `Float`, `Double`, `String`, `DateTime`, `Guid`.

#### **Subscriber (Prijímateľ)**

Trieda `OpcUaSubscriber` funguje na báze event-driven architektúry.

* **Asynchrónny príjem:** MQTT správy sú prijímané na samostatnom vlákne (Thread Pool), aby neblokovali hlavný renderovací cyklus Unity (Main Thread).
* **Diagnostika:** Obsahuje interný `StubLogger` a mechanizmy na zachytávanie chýb pri parsovaní (napr. `[Subscriber Error] JSON Parse`), čo uľahčuje debugging poškodených paketov.

---

## ⚙️ Inštalácia a Setup

Aby knižnica v prostredí Unity fungovala korektne, je nutné dodržať nasledujúce kroky.

### 1. Príprava Unity Projektu

Keďže DLL je postavená na `.NET Standard 2.1`, musíte zmeniť úroveň kompatibility API.

1. Choďte do **Edit** -> **Project Settings** -> **Player**.
2. V sekcii **Other Settings** -> **Configuration** nastavte:
* **Api Compatibility Level**: `.NET Standard 2.1` (odporúčané) alebo `.NET Framework`.


3. Uistite sa, že v projekte nie sú konfliktné verzie `Newtonsoft.Json.dll`.

### 2. Import Súborov

Do priečinka `Assets/Plugins` (ak neexistuje, vytvorte ho) nahrajte:

* `UnityOpcUaPublisher.dll` ako aj všetky ostatné .dll knižnice z priečinka Assets/Plugins z tohto Git repozitára

---

## 🚀 Implementácia: Publisher

Skript `ModularPublisherUI.cs` slúži ako wrapper, ktorý umožňuje konfigurovať odosielané dáta priamo cez Unity Inspector.

### Krok 1: Inicializácia a Definícia Schémy

Predtým, než začnete posielať dáta, musíte knižnici povedať, aké premenné (Dataset Fields) bude správa obsahovať. Na to slúži trieda `PublisherField`.

```csharp
using UnityOpcUaPublisher;

// Vytvorenie inštancie
OpcUaPublisher publisher = new OpcUaPublisher();

// Definícia dátovej štruktúry (Dataset)
List<PublisherField> schema = new List<PublisherField>();
schema.Add(new PublisherField("Temperature", "Float"));
schema.Add(new PublisherField("IsActive", "Boolean"));

// Štart publikovania (Broker URL + Topic)
// Príklad URL: mqtt://broker.hivemq.com:1883
publisher.Start("mqtt://localhost:1883", "Machine/Sensors", schema);

```

### Krok 2: Odosielanie hodnôt (Runtime)

Metóda `PublishValue` je optimalizovaná pre volanie v `Update()` slučke, avšak odporúča sa posielať dáta len pri zmene hodnoty pre šetrenie šírky pásma.

```csharp
// Hodnota musí typovo zodpovedať definícii v schéme!
publisher.PublishValue("Temperature", 42.5f);
publisher.PublishValue("IsActive", true);

```

*Poznámka: Pozri implementáciu metódy `OnSliderChanged` v `ModularPublisherUI.cs` pre príklad interaktívneho odosielania.*

---

## 📡 Implementácia: Subscriber

Príjem dát v Unity vyžaduje opatrné narábanie s vláknami (Threading). Callbacks z DLL knižnice prichádzajú z "pozadia", ale Unity UI API (napr. `text.Text = ...`) je možné volať **len z hlavného vlákna**.

Skript `SubscriberUI.cs` rieši tento problém pomocou vzoru **Thread-Safe Queue**.

### Krok 1: Prihlásenie na odber (Subscription)

```csharp
using UnityOpcUaPublisher;
using System.Collections.Concurrent;

// Fronta na prenos dát medzi vláknami
ConcurrentQueue<(string, object)> _queue = new ConcurrentQueue<(string, object)>();
OpcUaSubscriber subscriber = new OpcUaSubscriber();

// Event handler (beží na pozadí!)
subscriber.OnMessageReceived += (variableName, value) => {
    // Dáta len vložíme do fronty, nespracovávame tu UI
    _queue.Enqueue((variableName, value));
};

// Štart odberu
// TargetPublisherId musí sedieť s ID odosielateľa (default: "UnityPublisher")
List<string> variables = new List<string> { "Temperature", "IsActive" };
subscriber.Subscribe("mqtt://localhost:1883", "Machine/Sensors", "UnityPublisher", variables);

```

### Krok 2: Spracovanie dát (Main Thread)

V metóde `Update()` vyberáme dáta z fronty.

```csharp
void Update() {
    while (_queue.TryDequeue(out var data)) {
        string name = data.Item1;
        object val = data.Item2;

        // Teraz môžeme bezpečne aktualizovať UI
        if (name == "Temperature") {
            DisplayTemperature((float)val);
        }
    }
}

```

*Poznámka: Pozri implementáciu `SubscriberUI.cs` pre kompletný príklad s `ConcurrentQueue`.*

---

## 🛠 Riešenie problémov (Troubleshooting)

* **Chyba `DllNotFoundException`:**
* Skontrolujte, či je DLL fyzicky v priečinku `Assets/Plugins`.
* Reštartujte Unity Editor po pridaní DLL.


* **Dáta neodiaľujú / neprichádzajú:**
* Skontrolujte Firewall (Port 1883 pre MQTT).
* Overte, či sa `Topic` v Publisherovi a Subscriberovi presne zhoduje (Case Sensitive).
* Použite externý nástroj (napr. MQTT Explorer) na overenie, či dáta reálne chodia na broker.


* **Chyba `JsonReaderException`:**
* Znamená to, že prijaté dáta nie sú validný JSON alebo nezodpovedajú očakávanej OPC UA PubSub štruktúre. Skontrolujte formát správ v `RawDataReceivedEventArgs`.



---

## 📄 Licencia

Tento projekt (skripty `ModularPublisherUI.cs`, `SubscriberUI.cs` a súvisiaci kód v tomto repozitári) je licencovaný pod **MIT licenciou** — pozri súbor [LICENSE](LICENSE) s plným znením.

Priečinok `Assets/Plugins` obsahuje binárne (.dll) knižnice tretích strán, ktoré tento projekt využíva, ale nevlastní, a ktoré podliehajú vlastným licenčným podmienkam ich autorov:

* `MQTTnet.dll` — MIT License
* `Newtonsoft.Json.dll` — MIT License
* `Opc.Ua.Core.dll`, `Opc.Ua.PubSub.dll`, `Opc.Ua.Security.Certificates.dll`, `Opc.Ua.Types.dll` — súčasť OPC Foundation .NET Standard Stacku, podlieha licenčným podmienkam OPC Foundation
* `Microsoft.Extensions.*.dll`, `Microsoft.Bcl.AsyncInterfaces.dll`, `System.*.dll` — súčasti .NET runtime knižníc (Microsoft, MIT License)

Pred komerčným nasadením odporúčame overiť licenčné podmienky OPC Foundation stacku samostatne, keďže tie sa spravujú mimo tohto repozitára.
