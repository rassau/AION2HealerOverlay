using Windows.Win32;
using Windows.Win32.Media.MediaFoundation;
using Windows.Win32.System.Com;

namespace Cloris.Aion2Flow.Services.Audio;

// The sound of any file Windows itself can play (mp3, m4a, an mp4 video's sound, wav, wma, flac, ...), read with Windows'
// Media Foundation and mixed down to mono 16-bit at the file's own sample rate. Nothing to install or ship with it.
internal static unsafe class AudioFileDecoder
{
    private const uint FirstAudioStream = unchecked((uint)MF_SOURCE_READER_CONSTANTS.MF_SOURCE_READER_FIRST_AUDIO_STREAM);
    private const uint AllStreams = unchecked((uint)MF_SOURCE_READER_CONSTANTS.MF_SOURCE_READER_ALL_STREAMS);
    private const uint EndOrError = (uint)(MF_SOURCE_READER_FLAG.MF_SOURCE_READERF_ENDOFSTREAM | MF_SOURCE_READER_FLAG.MF_SOURCE_READERF_ERROR);

    // At most the first `longest` of it: a whole film would take gigabytes. Throws when there's no sound to read.
    public static AudioClip Decode(string path, TimeSpan longest)
    {
        var com = PInvoke.CoInitializeEx(COINIT.COINIT_MULTITHREADED);
        PInvoke.MFStartup(PInvoke.MF_VERSION, PInvoke.MFSTARTUP_LITE).ThrowOnFailure();
        IMFSourceReader* reader = null;
        IMFMediaType* wanted = null;
        IMFMediaType* actual = null;
        try
        {
            PInvoke.MFCreateSourceReaderFromURL(path, null, &reader).ThrowOnFailure();
            reader->SetStreamSelection(AllStreams, false);
            reader->SetStreamSelection(FirstAudioStream, true);
            PInvoke.MFCreateMediaType(&wanted).ThrowOnFailure();
            Set(wanted, PInvoke.MF_MT_MAJOR_TYPE, PInvoke.MFMediaType_Audio);
            Set(wanted, PInvoke.MF_MT_SUBTYPE, PInvoke.MFAudioFormat_PCM);
            reader->SetCurrentMediaType(FirstAudioStream, null, wanted);
            reader->GetCurrentMediaType(FirstAudioStream, &actual);
            var rate = (int)Get(actual, PInvoke.MF_MT_AUDIO_SAMPLES_PER_SECOND);
            var channels = (int)Get(actual, PInvoke.MF_MT_AUDIO_NUM_CHANNELS);
            var bytesPerSample = (int)Get(actual, PInvoke.MF_MT_AUDIO_BITS_PER_SAMPLE) / 8;
            if (rate <= 0 || channels <= 0 || bytesPerSample is < 1 or > 4)
                throw new InvalidDataException($"Unexpected sound format: {rate} Hz, {channels} channels, {bytesPerSample * 8} bit.");

            var limit = (int)Math.Min(int.MaxValue, longest.TotalSeconds * rate);
            var samples = new short[Math.Min(limit, rate * 60)];
            var count = 0;
            while (count < limit)
            {
                uint stream, flags;
                long time;
                IMFSample* sample = null;
                reader->ReadSample(FirstAudioStream, 0, &stream, &flags, &time, &sample);
                if (sample is not null)
                    count = Append(sample, channels, bytesPerSample, limit, ref samples, count);
                if ((flags & EndOrError) != 0)
                    break;
            }

            return count > 0 ? new AudioClip(samples[..count], rate) : throw new InvalidDataException("The file has no sound.");
        }
        finally
        {
            if (actual is not null)
                actual->Release();
            if (wanted is not null)
                wanted->Release();
            if (reader is not null)
                reader->Release();
            PInvoke.MFShutdown();
            if (com.Succeeded)
                PInvoke.CoUninitialize();
        }
    }

    // A sample's frames, each mixed down to one value, added after count; returns the new count.
    private static int Append(IMFSample* sample, int channels, int bytesPerSample, int limit, ref short[] samples, int count)
    {
        IMFMediaBuffer* buffer = null;
        try
        {
            sample->ConvertToContiguousBuffer(&buffer);
            byte* data;
            uint max, length;
            buffer->Lock(&data, &max, &length);
            try
            {
                var frameBytes = channels * bytesPerSample;
                for (var frame = 0; frame + frameBytes <= length && count < limit; frame += frameBytes)
                {
                    var sum = 0;
                    for (var channel = 0; channel < channels; channel++)
                        sum += Read(data + frame + channel * bytesPerSample, bytesPerSample);
                    if (count == samples.Length)
                        Array.Resize(ref samples, (int)Math.Min(limit, samples.Length * 2L));
                    samples[count++] = (short)(sum / channels);
                }
            }
            finally
            {
                buffer->Unlock();
            }
        }
        finally
        {
            if (buffer is not null)
                buffer->Release();
            sample->Release();
        }

        return count;
    }

    // One PCM value as 16-bit: 8-bit is unsigned, the wider ones keep their top 16 bits.
    private static int Read(byte* at, int bytes) => bytes switch
    {
        1 => (at[0] - 128) << 8,
        2 => *(short*)at,
        3 => (at[0] | at[1] << 8 | (sbyte)at[2] << 16) >> 8,
        _ => *(int*)at >> 16,
    };

    private static void Set(IMFMediaType* type, Guid key, Guid value) => type->SetGUID(&key, &value);

    private static uint Get(IMFMediaType* type, Guid key)
    {
        uint value;
        type->GetUINT32(&key, &value);
        return value;
    }
}
