using System.Drawing;
using System.Runtime.InteropServices;

namespace ChristianSpellHelper;

internal sealed class ClipboardPreserver : IDisposable
{
    private readonly DataObject _snapshot;
    private bool _restored;
    private bool _restoreOnDispose = true;

    public ClipboardPreserver()
    {
        System.Windows.Forms.IDataObject? source = null;
        RetryClipboard(() => source = Clipboard.GetDataObject());
        _snapshot = Materialize(source);
    }

    public void Restore()
    {
        if (_restored)
        {
            return;
        }

        RetryClipboard(() => Clipboard.SetDataObject(_snapshot, copy: true));
        _restored = true;
    }

    public void Abandon()
    {
        _restoreOnDispose = false;
    }

    public void Dispose()
    {
        if (_restoreOnDispose)
        {
            Restore();
        }

        GC.SuppressFinalize(this);
    }

    private static DataObject Materialize(System.Windows.Forms.IDataObject? source)
    {
        var copy = new DataObject();
        if (source is null)
        {
            return copy;
        }

        foreach (var format in source.GetFormats(autoConvert: false).Distinct(StringComparer.Ordinal))
        {
            try
            {
                var value = source.GetData(format, autoConvert: false);
                if (value is not null)
                {
                    copy.SetData(format, autoConvert: false, CloneClipboardValue(value));
                }
            }
            catch (ExternalException exception)
            {
                LocalLog.Write($"Could not materialize clipboard format '{format}'", exception);
            }
        }

        return copy;
    }

    private static object CloneClipboardValue(object value) => value switch
    {
        MemoryStream memory => new MemoryStream(memory.ToArray(), writable: false),
        Stream stream => CopyStream(stream),
        byte[] bytes => bytes.ToArray(),
        string[] paths => paths.ToArray(),
        Bitmap bitmap => bitmap.Clone(),
        Image image => image.Clone(),
        _ => value,
    };

    private static MemoryStream CopyStream(Stream source)
    {
        var originalPosition = source.CanSeek ? source.Position : 0;
        if (source.CanSeek)
        {
            source.Position = 0;
        }

        var copy = new MemoryStream();
        source.CopyTo(copy);
        copy.Position = 0;
        if (source.CanSeek)
        {
            source.Position = originalPosition;
        }

        return copy;
    }

    private static void RetryClipboard(Action action)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                action();
                return;
            }
            catch (ExternalException) when (attempt < 19)
            {
                Application.DoEvents();
                Thread.Sleep(Math.Min(10 * (attempt + 1), 50));
            }
        }
    }
}
