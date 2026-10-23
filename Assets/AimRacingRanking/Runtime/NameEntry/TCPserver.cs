/*
 * ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
 * @file    TcpServer.cs
 * @brief   Android端末からプレイヤー名を受信するTCPサーバー
 *
 * PC Unity側で動作
 *
 * 通信ポート:
 *     5000
 *
 * Android側接続先:
 *     127.0.0.1:5000
 *
 * ADB Reverse:
 *     adb reverse tcp:5000 tcp:5000
 *
 * 通信形式:
 *
 * Android → PC
 *     NAME|プレイヤー名
 *
 * PC → Android
 *     ACCEPTED
 *
 * ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
 */

using AimRacing.Ranking;
using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

// System.Diagnostics.Debugとの名前衝突を防ぐ
using Process = System.Diagnostics.Process;
using ProcessStartInfo = System.Diagnostics.ProcessStartInfo;


public class TcpServer : MonoBehaviour
{
    // ============================================================
    // Singleton
    // ============================================================

    public static TcpServer Instance { get; private set; }


    // ============================================================
    // イベント
    // ============================================================

    // 名前を受信しゲーム側へ反映した直後に、受信したプレイヤー名を引数として発火する
    public static event Action<string> OnNameReceived;


    // ============================================================
    // TCP設定
    // ============================================================

    [Header("TCP Settings")]

    [SerializeField]
    private int port = 5000;


    // ============================================================
    // ADB設定
    // ============================================================

    [Header("ADB Settings")]

    [Tooltip(
        "使用するAndroid端末のシリアル番号を入力してください。\n" +
        "空欄の場合はADB接続されている端末を自動検出します。"
    )]
    [SerializeField]
    private string deviceSerial = "HA2MPJ5P";


    // Unityに付属しているADB
    private const string AdbPath =
        @"C:\Program Files\Unity\Hub\Editor\6000.3.11f1\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools\adb.exe";


    // ============================================================
    // TCP
    // ============================================================

    private TcpListener server;

    private TcpClient client;

    private NetworkStream stream;


    // ============================================================
    // Thread
    // ============================================================

    private Thread serverThread;

    private Thread receiveThread;


    // ============================================================
    // 状態
    // ============================================================

    private volatile bool running = false;

    private volatile bool receiving = false;


    // ============================================================
    // 受信メッセージ
    // ============================================================

    private readonly ConcurrentQueue<string> messageQueue =
        new ConcurrentQueue<string>();


    // ============================================================
    // Unity Awake
    // ============================================================

    private void Awake()
    {
        // --------------------------------------------------------
        // Singleton
        // --------------------------------------------------------

        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;


        // --------------------------------------------------------
        // シーンを跨いでも残す
        // --------------------------------------------------------

        DontDestroyOnLoad(gameObject);
    }


    // ============================================================
    // Unity Update
    // ============================================================

    private void Update()
    {
        // --------------------------------------------------------
        // 別スレッドで受信したデータを
        // Unityメインスレッドで処理する
        // --------------------------------------------------------

        while (messageQueue.TryDequeue(out string message))
        {
            ProcessMessage(message);
        }
    }


    // ============================================================
    // 名前入力用サーバー開始
    // ============================================================

    public void StartNameEntryServer()
    {
        // --------------------------------------------------------
        // すでに起動している場合
        // --------------------------------------------------------

        if (running)
        {
            AppLog.Log(
                "[NameEntry] TCP Serverはすでに起動しています"
            );

            return;
        }


        AppLog.Log(
            "[NameEntry] ADB Reverse設定を開始します"
        );


        // --------------------------------------------------------
        // ADB Reverse設定
        // --------------------------------------------------------

        if (!SetupAdbReverse())
        {
            AppLog.LogError(
                "[NameEntry] ADB Reverseの設定に失敗しました"
            );

            return;
        }


        // --------------------------------------------------------
        // TCP Server開始
        // --------------------------------------------------------

        StartServer();
    }


    // ============================================================
    // ADB Reverse設定
    // ============================================================

    private bool SetupAdbReverse()
    {
        try
        {
            AppLog.Log(
                "[NameEntry] ADB端末を確認します"
            );


            // ====================================================
            // シリアル番号が指定されている場合
            // ====================================================

            if (!string.IsNullOrWhiteSpace(deviceSerial))
            {
                deviceSerial =
                    deviceSerial.Trim();


                AppLog.Log(
                    "[NameEntry] 使用するADB端末: " +
                    deviceSerial
                );


                // ------------------------------------------------
                // 指定した端末が存在するか確認
                // ------------------------------------------------

                if (!CheckSpecifiedDevice())
                {
                    AppLog.LogError(
                        "[NameEntry] 指定されたADB端末が見つかりません: " +
                        deviceSerial
                    );

                    return false;
                }
            }
            else
            {
                // =================================================
                // シリアル番号が空欄の場合
                // → ADB端末を自動検出
                // =================================================

                AppLog.Log(
                    "[NameEntry] シリアル番号が未指定のため、" +
                    "ADB端末を自動検出します"
                );


                int deviceCount =
                    GetAdbDeviceCount();


                if (deviceCount == 0)
                {
                    AppLog.LogError(
                        "[NameEntry] ADBで認識されている端末がありません"
                    );

                    AppLog.LogError(
                        "[NameEntry] Android端末をUSB接続し、" +
                        "USBデバッグを有効にしてください"
                    );

                    return false;
                }


                if (deviceCount > 1)
                {
                    AppLog.LogError(
                        "[NameEntry] ADB端末が複数接続されています"
                    );

                    AppLog.LogError(
                        "[NameEntry] 複数端末を使用する場合は、" +
                        "InspectorのDevice Serialにシリアル番号を指定してください"
                    );

                    return false;
                }


                AppLog.Log(
                    "[NameEntry] ADB端末を1台検出しました"
                );
            }


            // ====================================================
            // adb reverse
            // ====================================================

            AppLog.Log(
                "[NameEntry] adb reverseを実行します"
            );


            string reverseArguments;


            // ----------------------------------------------------
            // シリアル番号あり
            // ----------------------------------------------------

            if (!string.IsNullOrWhiteSpace(deviceSerial))
            {
                reverseArguments =
                    $"-s {deviceSerial} reverse tcp:{port} tcp:{port}";
            }
            else
            {
                // ------------------------------------------------
                // シリアル番号なし
                // ------------------------------------------------

                reverseArguments =
                    $"reverse tcp:{port} tcp:{port}";
            }


            ProcessStartInfo reversePsi =
                new ProcessStartInfo
                {
                    FileName = AdbPath,

                    Arguments = reverseArguments,

                    UseShellExecute = false,

                    RedirectStandardOutput = true,

                    RedirectStandardError = true,

                    CreateNoWindow = true
                };


            using (Process reverseProcess =
                   Process.Start(reversePsi))
            {
                if (reverseProcess == null)
                {
                    AppLog.LogError(
                        "[NameEntry] adb reverseを起動できません"
                    );

                    return false;
                }


                string output =
                    reverseProcess.StandardOutput.ReadToEnd();


                string error =
                    reverseProcess.StandardError.ReadToEnd();


                reverseProcess.WaitForExit();


                AppLog.Log(
                    "[NameEntry] adb reverse output: " +
                    output
                );


                if (!string.IsNullOrEmpty(error))
                {
                    Debug.LogWarning(
                        "[NameEntry] adb reverse error: " +
                        error
                    );
                }


                if (reverseProcess.ExitCode != 0)
                {
                    AppLog.LogError(
                        "[NameEntry] adb reverseに失敗しました"
                    );

                    return false;
                }
            }


            // ====================================================
            // Reverse設定確認
            // ====================================================

            string checkArguments;


            if (!string.IsNullOrWhiteSpace(deviceSerial))
            {
                checkArguments =
                    $"-s {deviceSerial} reverse --list";
            }
            else
            {
                checkArguments =
                    "reverse --list";
            }


            ProcessStartInfo checkPsi =
                new ProcessStartInfo
                {
                    FileName = AdbPath,

                    Arguments = checkArguments,

                    UseShellExecute = false,

                    RedirectStandardOutput = true,

                    RedirectStandardError = true,

                    CreateNoWindow = true
                };


            using (Process checkProcess =
                   Process.Start(checkPsi))
            {
                if (checkProcess == null)
                {
                    AppLog.LogError(
                        "[NameEntry] reverse確認に失敗しました"
                    );

                    return false;
                }


                string list =
                    checkProcess.StandardOutput.ReadToEnd();


                string error =
                    checkProcess.StandardError.ReadToEnd();


                checkProcess.WaitForExit();


                AppLog.Log(
                    "[NameEntry] adb reverse --list:\n" +
                    list
                );


                if (!string.IsNullOrEmpty(error))
                {
                    Debug.LogWarning(
                        "[NameEntry] reverse --list error:\n" +
                        error
                    );
                }


                string expected =
                    $"tcp:{port} tcp:{port}";


                if (!list.Contains(expected))
                {
                    AppLog.LogError(
                        "[NameEntry] TCP Reverseが確認できません"
                    );

                    AppLog.LogError(
                        "[NameEntry] 必要な設定: " +
                        expected
                    );

                    return false;
                }
            }


            AppLog.Log(
                "[NameEntry] ADB Reverse設定成功"
            );


            return true;
        }
        catch (Exception e)
        {
            AppLog.LogError(
                "[NameEntry] ADB Reverse例外: " +
                e
            );

            return false;
        }
    }


    // ============================================================
    // 指定されたADB端末の確認
    // ============================================================

    private bool CheckSpecifiedDevice()
    {
        try
        {
            ProcessStartInfo psi =
                new ProcessStartInfo
                {
                    FileName = AdbPath,

                    Arguments =
                        $"-s {deviceSerial} get-state",

                    UseShellExecute = false,

                    RedirectStandardOutput = true,

                    RedirectStandardError = true,

                    CreateNoWindow = true
                };


            using (Process process =
                   Process.Start(psi))
            {
                if (process == null)
                {
                    return false;
                }


                string output =
                    process.StandardOutput.ReadToEnd();


                string error =
                    process.StandardError.ReadToEnd();


                process.WaitForExit();


                AppLog.Log(
                    "[NameEntry] 指定端末の状態: " +
                    output.Trim()
                );


                if (
                    process.ExitCode == 0 &&
                    output.Trim() == "device"
                )
                {
                    return true;
                }


                if (!string.IsNullOrEmpty(error))
                {
                    Debug.LogWarning(
                        "[NameEntry] ADB状態確認エラー: " +
                        error
                    );
                }


                return false;
            }
        }
        catch (Exception e)
        {
            AppLog.LogError(
                "[NameEntry] ADB端末確認例外: " +
                e.Message
            );

            return false;
        }
    }


    // ============================================================
    // ADB端末数取得
    // ============================================================

    private int GetAdbDeviceCount()
    {
        try
        {
            ProcessStartInfo psi =
                new ProcessStartInfo
                {
                    FileName = AdbPath,

                    Arguments = "devices",

                    UseShellExecute = false,

                    RedirectStandardOutput = true,

                    RedirectStandardError = true,

                    CreateNoWindow = true
                };


            using (Process process =
                   Process.Start(psi))
            {
                if (process == null)
                {
                    return 0;
                }


                string output =
                    process.StandardOutput.ReadToEnd();


                string error =
                    process.StandardError.ReadToEnd();


                process.WaitForExit();


                AppLog.Log(
                    "[NameEntry] adb devices:\n" +
                    output
                );


                if (!string.IsNullOrEmpty(error))
                {
                    Debug.LogWarning(
                        "[NameEntry] adb devices error:\n" +
                        error
                    );
                }


                return CountAdbDevices(output);
            }
        }
        catch (Exception e)
        {
            AppLog.LogError(
                "[NameEntry] adb devices実行例外: " +
                e.Message
            );

            return 0;
        }
    }


    // ============================================================
    // ADB端末数を数える
    // ============================================================

    private int CountAdbDevices(string output)
    {
        if (string.IsNullOrEmpty(output))
        {
            return 0;
        }


        int count = 0;


        string[] lines =
            output.Split(
                new[] { '\r', '\n' },
                StringSplitOptions.RemoveEmptyEntries
            );


        foreach (string line in lines)
        {
            string trimmed =
                line.Trim();


            if (
                string.IsNullOrEmpty(trimmed) ||
                trimmed.StartsWith("List of devices attached")
            )
            {
                continue;
            }


            if (trimmed.EndsWith("\tdevice"))
            {
                count++;
            }
        }


        return count;
    }


    // ============================================================
    // TCP Server開始
    // ============================================================

    private void StartServer()
    {
        if (running)
        {
            return;
        }


        running = true;


        serverThread =
            new Thread(ServerLoop);


        serverThread.IsBackground = true;


        serverThread.Start();


        AppLog.Log(
            $"[NameEntry] TCP Server起動 Port:{port}"
        );
    }


    // ============================================================
    // Server Loop
    // ============================================================

    private void ServerLoop()
    {
        try
        {
            server =
                new TcpListener(
                    IPAddress.Any,
                    port
                );


            server.Start();


            AppLog.Log(
                $"[NameEntry] TCP Server Listening : {port}"
            );


            while (running)
            {
                try
                {
                    AppLog.Log(
                        "[NameEntry] Androidからの接続を待機しています..."
                    );


                    TcpClient newClient =
                        server.AcceptTcpClient();


                    if (!running)
                    {
                        newClient.Close();
                        break;
                    }


                    CloseClient();


                    client =
                        newClient;


                    AppLog.Log(
                        "[NameEntry] Android端末が接続しました"
                    );


                    stream =
                        client.GetStream();


                    receiving = true;


                    receiveThread =
                        new Thread(ReceiveLoop);


                    receiveThread.IsBackground = true;


                    receiveThread.Start();
                }
                catch (SocketException e)
                {
                    if (running)
                    {
                        AppLog.LogError(
                            "[NameEntry] SocketException: " +
                            e.Message
                        );
                    }
                }
                catch (Exception e)
                {
                    if (running)
                    {
                        AppLog.LogError(
                            "[NameEntry] ServerLoop例外: " +
                            e.Message
                        );
                    }
                }
            }
        }
        catch (Exception e)
        {
            AppLog.LogError(
                "[NameEntry] ServerLoop起動失敗: " +
                e
            );
        }
    }


    // ============================================================
    // データ受信
    // ============================================================

    private void ReceiveLoop()
    {
        try
        {
            byte[] buffer =
                new byte[4096];


            while (
                running &&
                receiving
            )
            {
                if (stream == null)
                {
                    break;
                }


                int bytesRead =
                    stream.Read(
                        buffer,
                        0,
                        buffer.Length
                    );


                if (bytesRead <= 0)
                {
                    AppLog.Log(
                        "[NameEntry] Android端末との接続が切断されました"
                    );

                    break;
                }


                string message =
                    Encoding.UTF8.GetString(
                        buffer,
                        0,
                        bytesRead
                    );


                AppLog.Log(
                    "[NameEntry] Received: " +
                    message
                );


                messageQueue.Enqueue(message);
            }
        }
        catch (Exception e)
        {
            if (running)
            {
                Debug.LogWarning(
                    "[NameEntry] ReceiveLoop終了: " +
                    e.Message
                );
            }
        }
        finally
        {
            receiving = false;
        }
    }


    // ============================================================
    // 受信データ処理
    // ============================================================

    private void ProcessMessage(string message)
    {
        if (string.IsNullOrEmpty(message))
        {
            return;
        }


        // --------------------------------------------------------
        // NAME|プレイヤー名
        // --------------------------------------------------------

        if (message.StartsWith("NAME|"))
        {
            string playerName =
                message.Substring(5);


            playerName =
                playerName.Trim();


            AppLog.Log(
                "[NameEntry] プレイヤー名を受信: " +
                playerName
            );


            if (string.IsNullOrEmpty(playerName))
            {
                Debug.LogWarning(
                    "[NameEntry] プレイヤー名が空です"
                );

                return;
            }


            // ----------------------------------------------------
            // ゲーム側へ名前を渡す
            // ----------------------------------------------------

            AimRacingPlayerName.SetName(
                playerName
            );


            AppLog.Log(
                "[NameEntry] プレイヤー名を設定しました: " +
                playerName
            );


            // ----------------------------------------------------
            // 受信完了をゲーム側(MenuState等)へ通知
            // ----------------------------------------------------

            OnNameReceived?.Invoke(playerName);


            // ----------------------------------------------------
            // Androidへ受付通知
            // ----------------------------------------------------

            Send("ACCEPTED");
        }
    }


    // ============================================================
    // Androidへ送信
    // ============================================================

    public void Send(string message)
    {
        if (stream == null)
        {
            Debug.LogWarning(
                "[NameEntry] Androidとの接続がありません"
            );

            return;
        }


        try
        {
            byte[] data =
                Encoding.UTF8.GetBytes(
                    message
                );


            stream.Write(
                data,
                0,
                data.Length
            );


            stream.Flush();


            AppLog.Log(
                "[NameEntry] Sent: " +
                message
            );
        }
        catch (Exception e)
        {
            AppLog.LogError(
                "[NameEntry] Send失敗: " +
                e.Message
            );
        }
    }


    // ============================================================
    // Client終了
    // ============================================================

    private void CloseClient()
    {
        receiving = false;


        try
        {
            stream?.Close();
        }
        catch
        {
        }


        stream = null;


        try
        {
            client?.Close();
        }
        catch
        {
        }


        client = null;
    }


    // ============================================================
    // Server停止
    // ============================================================

    public void StopNameEntryServer()
    {
        AppLog.Log(
            "[NameEntry] TCP Serverを停止します"
        );


        running = false;


        receiving = false;


        CloseClient();


        try
        {
            server?.Stop();
        }
        catch
        {
        }


        server = null;


        try
        {
            if (
                serverThread != null &&
                serverThread.IsAlive
            )
            {
                serverThread.Join(500);
            }
        }
        catch
        {
        }


        serverThread = null;


        RemoveAdbReverse();
    }


    // ============================================================
    // ADB Reverse解除
    // ============================================================

    private void RemoveAdbReverse()
    {
        try
        {
            string arguments;


            if (!string.IsNullOrWhiteSpace(deviceSerial))
            {
                arguments =
                    $"-s {deviceSerial} reverse remove tcp:{port}";
            }
            else
            {
                arguments =
                    $"reverse remove tcp:{port}";
            }


            ProcessStartInfo psi =
                new ProcessStartInfo
                {
                    FileName = AdbPath,

                    Arguments = arguments,

                    UseShellExecute = false,

                    RedirectStandardOutput = true,

                    RedirectStandardError = true,

                    CreateNoWindow = true
                };


            using (Process process =
                   Process.Start(psi))
            {
                if (process == null)
                {
                    return;
                }


                string output =
                    process.StandardOutput.ReadToEnd();


                string error =
                    process.StandardError.ReadToEnd();


                process.WaitForExit();


                AppLog.Log(
                    "[NameEntry] ADB Reverseを解除しました"
                );


                if (!string.IsNullOrEmpty(output))
                {
                    AppLog.Log(
                        "[NameEntry] reverse remove output: " +
                        output
                    );
                }


                if (!string.IsNullOrEmpty(error))
                {
                    Debug.LogWarning(
                        "[NameEntry] reverse remove error: " +
                        error
                    );
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning(
                "[NameEntry] ADB Reverse解除失敗: " +
                e.Message
            );
        }
    }


    // ============================================================
    // Unity終了
    // ============================================================

    private void OnDestroy()
    {
        if (Instance == this)
        {
            StopNameEntryServer();

            Instance = null;
        }
    }


    private void OnApplicationQuit()
    {
        if (Instance == this)
        {
            StopNameEntryServer();
        }
    }
}