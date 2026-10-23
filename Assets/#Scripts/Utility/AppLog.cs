/*━━━━━━━━━
@file    AppLog.cs
@brief   Debug.Logのラッパー。Logはリリースビルドで呼び出しごと除去し、LogWarning/LogErrorはリリースでも出力する
@author  24CU0110 大川翔生
@date    作成日   : 2026/09/03
         最終更新日 : 2026/10/20
━━━━━━━━━*/

using UnityEngine;

/// <summary>
/// UnityEngine.Debugの呼び出しをラップするユーティリティ。
/// Logはリリースビルドでは出力自体を消去する。
/// LogWarning/LogErrorはリリースビルドでも出力する(展示で不具合が起きたときに、原因をPlayer.logに残すため)。
/// </summary>
public static class AppLog
{
    /// <summary>
    /// 通常ログを出力します。リリースビルドでは呼び出しごと除去されます。
    /// </summary>
    /// <param name="_message">出力するメッセージ</param>
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    public static void Log(object _message)
    {
        Debug.Log(_message);
    }

    /// <summary>
    /// 通常ログをコンテキスト付きで出力します。リリースビルドでは呼び出しごと除去されます。
    /// </summary>
    /// <param name="_message">出力するメッセージ</param>
    /// <param name="_context">Consoleクリック時にハイライトする対象オブジェクト</param>
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    public static void Log(object _message, Object _context)
    {
        Debug.Log(_message, _context);
    }

    /// <summary>
    /// 警告ログを出力します。リリースビルドでも出力されます。毎フレーム通る処理では、一度だけ出す・間引くなどして、ログがあふれないようにしてください。
    /// </summary>
    /// <param name="_message">出力するメッセージ</param>
    public static void LogWarning(object _message)
    {
        Debug.LogWarning(_message);
    }

    /// <summary>
    /// 警告ログをコンテキスト付きで出力します。リリースビルドでも出力されます。毎フレーム通る処理では、一度だけ出す・間引くなどして、ログがあふれないようにしてください。
    /// </summary>
    /// <param name="_message">出力するメッセージ</param>
    /// <param name="_context">Consoleクリック時にハイライトする対象オブジェクト</param>
    public static void LogWarning(object _message, Object _context)
    {
        Debug.LogWarning(_message, _context);
    }

    /// <summary>
    /// エラーログを出力します。リリースビルドでも出力されます。毎フレーム通る処理では、一度だけ出す・間引くなどして、ログがあふれないようにしてください。
    /// </summary>
    /// <param name="_message">出力するメッセージ</param>
    public static void LogError(object _message)
    {
        Debug.LogError(_message);
    }

    /// <summary>
    /// エラーログをコンテキスト付きで出力します。リリースビルドでも出力されます。毎フレーム通る処理では、一度だけ出す・間引くなどして、ログがあふれないようにしてください。
    /// </summary>
    /// <param name="_message">出力するメッセージ</param>
    /// <param name="_context">Consoleクリック時にハイライトする対象オブジェクト</param>
    public static void LogError(object _message, Object _context)
    {
        Debug.LogError(_message, _context);
    }
}
