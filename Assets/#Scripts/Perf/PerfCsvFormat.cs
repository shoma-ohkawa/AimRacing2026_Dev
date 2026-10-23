/*━━━━━━━━━
@file    PerfCsvFormat.cs
@brief   計測用: CSVとJSONの文字列を、ガベージを出さずに(または最小限で)組み立てる関数群
@author  24CU0110 大川翔生
@date    作成日   : 2026/10/16
         最終更新日 : 2026/10/19
━━━━━━━━━*/

using System;
using System.Text;

//数値を StringBuilder へ直接書き込む。ロケールに依存せず、計測中に文字列を作らない(GCの計測を汚さないため)
public static class PerfCsvFormat
{
    private const long k_Base = 10; //10進数の基数
    private const string k_Missing = "-1"; //取得できなかった値の表記
    private const double k_MaxScaledValue = 9.0e18; //long で表せる範囲の上限(これを超える値は取得不可として扱う)
    private const int k_DateYearWidth = 4; //日時の年の桁数
    private const int k_DateFieldWidth = 2; //日時の月・日・時・分・秒の桁数
    private const int k_MaxDecimals = 6; //小数の桁数の上限

    private static readonly long[] k_PowersOfTen = { 1L, 10L, 100L, 1000L, 10000L, 100000L, 1000000L }; //10のべき乗(小数の桁数ごと)

    /// <summary>
    /// 整数を書き込みます。
    /// </summary>
    /// <param name="_builder">書き込み先</param>
    /// <param name="_value">書き込む整数</param>
    public static void AppendInteger(StringBuilder _builder, long _value)
    {
        if (_value < 0)
        {
            _builder.Append('-');
            _value = -_value;
        }

        //最上位の桁を表す除数を求める処理
        long divisor = 1;
        while (_value / divisor >= k_Base) { divisor *= k_Base; }

        //上の桁から1桁ずつ書き込む処理
        while (divisor > 0)
        {
            long digit = _value / divisor;
            _builder.Append((char)('0' + digit));
            _value -= digit * divisor;
            divisor /= k_Base;
        }
    }

    /// <summary>
    /// 小数を、指定した桁数で四捨五入して書き込みます。負の値・非数・無限大は「取得できなかった値」として -1 を書きます。
    /// </summary>
    /// <param name="_builder">書き込み先</param>
    /// <param name="_value">書き込む値(0以上)</param>
    /// <param name="_decimals">小数の桁数(0〜6)</param>
    public static void AppendValue(StringBuilder _builder, double _value, int _decimals)
    {
        if (double.IsNaN(_value) || double.IsInfinity(_value) || _value < 0.0)
        {
            _builder.Append(k_Missing);
            return;
        }

        int decimals = Math.Min(Math.Max(_decimals, 0), k_MaxDecimals);
        long power = k_PowersOfTen[decimals];
        double scaledDouble = _value * power;

        if (scaledDouble > k_MaxScaledValue)
        {
            _builder.Append(k_Missing);
            return;
        }

        long scaled = (long)Math.Round(scaledDouble, MidpointRounding.AwayFromZero);
        AppendInteger(_builder, scaled / power);

        if (decimals == 0) { return; }

        _builder.Append('.');

        //小数部を、上の桁から0埋めして書き込む処理
        long fraction = scaled % power;
        long divisor = power / k_Base;
        while (divisor > 0)
        {
            long digit = fraction / divisor;
            _builder.Append((char)('0' + digit));
            fraction -= digit * divisor;
            divisor /= k_Base;
        }
    }

    /// <summary>
    /// 取得できなかった値(-1)を書き込みます。
    /// </summary>
    /// <param name="_builder">書き込み先</param>
    public static void AppendMissing(StringBuilder _builder)
    {
        _builder.Append(k_Missing);
    }

    /// <summary>
    /// 文字列を、CSVの1つのセルとして書き込みます。区切りのカンマと改行は、新しい文字列を作らずに「_」へ置き換えます。
    /// </summary>
    /// <param name="_builder">書き込み先</param>
    /// <param name="_text">書き込む文字列(nullは空として扱う)</param>
    public static void AppendText(StringBuilder _builder, string _text)
    {
        if (_text == null) { return; }

        //1文字ずつ、区切りになる文字を置き換えながら書き込む処理
        for (int i = 0; i < _text.Length; ++i)
        {
            char c = _text[i];
            _builder.Append(c == ',' || c == '\r' || c == '\n' ? '_' : c);
        }
    }

    /// <summary>
    /// 日時を "yyyy-MM-ddTHH:mm:ss" の形で書き込みます。
    /// </summary>
    /// <param name="_builder">書き込み先</param>
    /// <param name="_time">書き込む日時</param>
    public static void AppendDateTime(StringBuilder _builder, DateTime _time)
    {
        AppendPadded(_builder, _time.Year, k_DateYearWidth);
        _builder.Append('-');
        AppendPadded(_builder, _time.Month, k_DateFieldWidth);
        _builder.Append('-');
        AppendPadded(_builder, _time.Day, k_DateFieldWidth);
        _builder.Append('T');
        AppendPadded(_builder, _time.Hour, k_DateFieldWidth);
        _builder.Append(':');
        AppendPadded(_builder, _time.Minute, k_DateFieldWidth);
        _builder.Append(':');
        AppendPadded(_builder, _time.Second, k_DateFieldWidth);
    }

    /// <summary>
    /// 整数を、指定した桁数になるよう0で埋めて書き込みます。
    /// </summary>
    /// <param name="_builder">書き込み先</param>
    /// <param name="_value">書き込む整数(0以上)</param>
    /// <param name="_width">桁数</param>
    private static void AppendPadded(StringBuilder _builder, int _value, int _width)
    {
        long divisor = 1;

        //桁数に応じた除数を求める処理
        for (int i = 1; i < _width; ++i) { divisor *= k_Base; }

        //上の桁から1桁ずつ書き込む処理
        while (divisor > 0)
        {
            long digit = (_value / divisor) % k_Base;
            _builder.Append((char)('0' + digit));
            divisor /= k_Base;
        }
    }

    /// <summary>
    /// JSONの文字列として使えるよう、エスケープして引用符で囲んだ文字列を返します。
    /// </summary>
    /// <param name="_text">元の文字列</param>
    public static string JsonString(string _text)
    {
        if (_text == null) { _text = string.Empty; }

        StringBuilder builder = new StringBuilder(_text.Length + 2);
        builder.Append('"');

        //1文字ずつエスケープする処理
        for (int i = 0; i < _text.Length; ++i)
        {
            char c = _text[i];
            if (c == '"' || c == '\\') { builder.Append('\\').Append(c); }
            else if (c == '\n') { builder.Append("\\n"); }
            else if (c == '\r') { builder.Append("\\r"); }
            else if (c == '\t') { builder.Append("\\t"); }
            else if (c < ' ') { builder.Append(' '); }
            else { builder.Append(c); }
        }

        return builder.Append('"').ToString();
    }

    /// <summary>
    /// フォルダ名・ファイル名に使えない文字を「_」に置き換えた文字列を返します。
    /// </summary>
    /// <param name="_text">元の文字列</param>
    public static string SanitizeFileName(string _text)
    {
        if (string.IsNullOrEmpty(_text)) { return string.Empty; }

        char[] invalid = System.IO.Path.GetInvalidFileNameChars();
        StringBuilder builder = new StringBuilder(_text.Length);

        //使えない文字を1文字ずつ置き換える処理
        for (int i = 0; i < _text.Length; ++i)
        {
            builder.Append(Array.IndexOf(invalid, _text[i]) >= 0 ? '_' : _text[i]);
        }

        return builder.ToString();
    }
}
