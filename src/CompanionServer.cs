using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;

namespace AcrFfbGain
{
    /// <summary>
    /// Local request channel for companion apps (MozaHud): a named pipe "ACRallyStageSwitch", one request line,
    /// one reply line, fields separated by tabs.
    ///   HELLO                                   -> OK  StageSwitch  version
    ///   GET     rally  stage  car               -> OK  rallyName stageName rallyGain stageGain carGain(- = none) carName perCar(0/1)
    ///   SETGAIN scope  rally  stage  car  gain   -> OK  message
    ///           scope: car (this car, this stage) | rallycar (this car, all the rally's stages)
    ///                  | stage (all cars, this stage) | rally (all cars, all the rally's stages)
    /// Errors: ERR message. Requests run on the UI thread (the handler), so they go through the app's own logic.
    /// </summary>
    public sealed class CompanionServer
    {
        public const string PipeName = "ACRallyStageSwitch";
        readonly Func<string[], string> _handle;
        readonly Thread _thread;
        volatile bool _stop;

        public CompanionServer(Func<string[], string> handle)
        {
            _handle = handle;
            _thread = new Thread(Run) { IsBackground = true, Name = "companion-pipe" };
            _thread.Start();
        }

        void Run()
        {
            while (!_stop)
            {
                try
                {
                    using (var pipe = new NamedPipeServerStream(PipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.None))
                    {
                        pipe.WaitForConnection();
                        var reader = new StreamReader(pipe, new UTF8Encoding(false));
                        var writer = new StreamWriter(pipe, new UTF8Encoding(false)) { AutoFlush = true };
                        string line = reader.ReadLine();
                        string reply;
                        try { reply = line == null ? "ERR empty request" : _handle(line.Split('\t')); }
                        catch (Exception ex) { reply = "ERR " + ex.Message; }
                        writer.WriteLine((reply ?? "ERR no reply").Replace('\n', ' ').Replace('\r', ' '));
                        try { pipe.WaitForPipeDrain(); } catch { }
                    }
                }
                catch (Exception ex)
                {
                    if (_stop) return;
                    Log.Write("Companion pipe: " + ex.Message);
                    Thread.Sleep(1000);
                }
            }
        }

        public void Stop() { _stop = true; }
    }
}
