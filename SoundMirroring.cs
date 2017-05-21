using UnityEngine;
using CSCore;
using CSCore.SoundIn;
using CSCore.Streams;
using System;
using CSCore.SoundOut;
using CSCore.CoreAudioAPI;
using System.Runtime.InteropServices;
using System.Collections.Generic;

public class SoundMirroring : MonoBehaviour
{
    public WasapiCapture capture;
    public IWaveSource actualSource;
    PureDataSource dataSource;
    WasapiOut games;

    public enum ConverterQuality
    {
        SRC_SINC_FASTEST = 2,
        SRC_SINC_MEDIUM_QUALITY = 1,
        SRC_SINC_BEST_QUALITY = 0
    };

    [DllImport("samplerate", EntryPoint = "src_simple_plain")]
    public static extern int src_simple_plain(float[] data_in, float[] data_out, int input_frames, int output_frames, float src_ratio, ConverterQuality converter_type, int channels);


    List<MMDevice> devices;

    private GUIStyle listStyle = new GUIStyle();
    void Start()
    {
        devices = new List<MMDevice>();
        var ayy = new MMDeviceEnumerator();
        foreach (MMDevice device in ayy.EnumAudioEndpoints(DataFlow.Render, DeviceState.Active))
        {
            devices.Add(device);
        }
    }

    public void BeginCapture(MMDevice device1, MMDevice device2)
    {

        // This uses the wasapi api to get any sound data played by the computer
        capture = new WasapiLoopbackCapture();
        capture.Device = device1;
        capture.Initialize();

        actualSource = new SoundInSource(capture);

        dataSource = new PureDataSource(new WaveFormat(device2.DeviceFormat.SampleRate, 8, 2), actualSource.ToSampleSource());

        capture.Start();

        games = new WasapiOut();
        games.Device = device2;
        games.Initialize(dataSource.ToWaveSource());

        isSetup = true;
    }


    bool cleanedUp = false;


    void Cleanup()
    {
        if (!cleanedUp)
        {
            cleanedUp = true;
            if (dataSource != null)
            {
                dataSource.source = null;
            }
            if (games != null && games.PlaybackState != PlaybackState.Stopped)
            {
                games.Stop();
                games.Dispose();
            }
            if (capture != null && capture.RecordingState == RecordingState.Recording)
            {
                capture.Stop();
            }
            if (capture != null)
            {
                capture.Dispose();
            }
        }
    }

    private void OnDestroy()
    {
        Cleanup();
    }


    void OnApplicationQuit()
    {
        Cleanup();
    }

    bool isStarted = false;
    bool isSetup = false;
    void Update()
    {
        if (!isStarted && isSetup)
        {
            isStarted = true;
            games.Play();
            dataSource.quality = quality;
        }
    }


    public class PureDataSource : ISampleSource
    {
        public long Length
        {
            get
            {
                return 0;
            }
        }

        public long Position
        {
            get
            {
                return 0;
            }

            set
            {
                throw new NotImplementedException();
            }
        }
        private WaveFormat _WaveFormat;
        public WaveFormat WaveFormat
        {
            get
            {
                return _WaveFormat;
            }
        }

        private int _Patch;
        public int Patch
        {
            get { return _Patch; }
            //set { _Patch = value; }
        }

        public bool CanSeek
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public ISampleSource source;

        public PureDataSource(WaveFormat waveFormat, ISampleSource source)
        {
            _WaveFormat = waveFormat;
            this.source = source;
        }

        public ConverterQuality quality;

        float[] tempBuffer1 = new float[80000 * 20];
        float[] tempBuffer2 = new float[80000 * 20];
        float lastThing = 0;
        public int Read(float[] buffer, int offset, int count)
        {
            int sourceSampleRate = source.WaveFormat.SampleRate;
            int mySampleRate = WaveFormat.SampleRate;

            int numTheirSamples = (int)Mathf.Floor((count * (float)sourceSampleRate / mySampleRate));
            if (source == null)
            {
                return 0;
            }
            int res = 100;
            try
            {
                res = source.Read(tempBuffer1, 0, numTheirSamples);
                if (res == 0)
                {
                    if (count == 0)
                    {
                        return 0;
                    }
                    if (count == 1)
                    {
                        buffer[offset] = lastThing;
                        return 1;
                    }
                    if (count >= 2)
                    {
                        buffer[offset] = lastThing*0.8f;
                        buffer[offset + 1] = lastThing*0.6f;
                        return 2;
                    }
                }
                int numOurSamples = (int)Mathf.Round((res * (float)mySampleRate / sourceSampleRate));
                res = src_simple_plain(tempBuffer1, tempBuffer2, res, numOurSamples, (float)mySampleRate / sourceSampleRate , quality, source.WaveFormat.Channels);
                if (res > count)
                {
                    res = count;
                }
                Buffer.BlockCopy(tempBuffer2, 0, buffer, offset, res * 4);
                lastThing = buffer[offset + res-1];
            }
            catch (Exception e)
            {
                Debug.Log("failed read: " + e.Message);
            }

            return Mathf.Max(res, 2);
        }

        public void Dispose()
        {

        }
    }

    public MMDevice device1;
    public MMDevice device2;

    public ConverterQuality quality = ConverterQuality.SRC_SINC_FASTEST;

    void OnGUI()
    {
        GUI.Box(new Rect(10, 40, 70, 25), "Quality:");

        if (quality == ConverterQuality.SRC_SINC_FASTEST)
        {
            GUI.Box(new Rect(80, 40, 70, 25), "Low");
        }
        else
        {
            if (GUI.Button(new Rect(80, 40, 70, 25), "Low"))
            {
                quality = ConverterQuality.SRC_SINC_FASTEST;
            }
        }
        if (quality == ConverterQuality.SRC_SINC_MEDIUM_QUALITY)
        {
            GUI.Box(new Rect(150, 40, 70, 25), "Medium");
        }
        else
        {
            if (GUI.Button(new Rect(150, 40, 70, 25), "Medium"))
            {
                quality = ConverterQuality.SRC_SINC_MEDIUM_QUALITY;
            }
        }

        if (quality == ConverterQuality.SRC_SINC_BEST_QUALITY)
        {
            GUI.Box(new Rect(220, 40, 70, 25), "High");
        }
        else
        {
            if (GUI.Button(new Rect(220, 40, 70, 25), "High"))
            {
                quality = ConverterQuality.SRC_SINC_BEST_QUALITY;
            }
        }

        int widthOfThings = 500;
        if (isSetup)
        {
            GUI.Box(new Rect(10, 10, widthOfThings, 20), "Mirroring sound on device 1 to device 2");
            string device1Texta = "Device 1";
            if (device1 != null)
            {
                device1Texta = "Device 1: " + device1.FriendlyName;
            }

            GUI.Box(new Rect(10, 70, widthOfThings, 20), device1Texta);

            string device2Texta = "Device 2";
            if (device2 != null)
            {
                device2Texta = "Device 2: " + device2.FriendlyName;
            }
            GUI.Box(new Rect(10, 95, widthOfThings, 20), device2Texta);
            return;
        }


        if (device1 != device2 && device1 != null && device2 != null)
        {
            if (GUI.Button(new Rect(10, 10, widthOfThings, 20), "Mirror sound on device 1 to device 2"))
            {
                BeginCapture(device1, device2);
            }
        }

       


        int offsetFromTop = 70;

        string device1Text = "Device 1";
        if (device1 != null)
        {
            device1Text = "Device 1: " + device1.FriendlyName;
        }

        GUI.Box(new Rect(10, 10+offsetFromTop, widthOfThings, 20), device1Text);

        string device2Text = "Device 2";
        if (device2 != null)
        {
            device2Text = "Device 2: "+ device2.FriendlyName;
        }
        GUI.Box(new Rect(10, 60 + devices.Count * 25+ offsetFromTop, widthOfThings, 20), device2Text);


        for (int i = 0; i < devices.Count;i++)
        {
            // Make the second button.
            if (GUI.Button(new Rect(10, i*25+40+ offsetFromTop, widthOfThings, 20), devices[i].FriendlyName))
            {
                device1 = devices[i];
            }
        }

        for (int i = 0; i < devices.Count; i++)
        {
            // Make the second button.
            if (GUI.Button(new Rect(10, i * 25 + 90+ devices.Count*25+ offsetFromTop, widthOfThings, 20), devices[i].FriendlyName))
            {
                device2 = devices[i];
            }
        }

    }

}
