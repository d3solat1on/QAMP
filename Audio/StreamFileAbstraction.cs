namespace QAMP.Audio;
public class StreamFileAbstraction : TagLib.File.IFileAbstraction
{
    public string Name { get; }
    public System.IO.Stream ReadStream { get; }
    public System.IO.Stream WriteStream { get; }

    public StreamFileAbstraction(string name, System.IO.Stream readStream, System.IO.Stream writeStream)
    {
        Name = name;
        ReadStream = readStream;
        WriteStream = writeStream;
    }

    public void CloseStream(System.IO.Stream stream)
    {
        // Поток закроется сам при выходе из using в основном коде
    }
}