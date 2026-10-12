"""Audio conversion: PSX VAG ADPCM (self-written decoder) and ATRAC3plus RIFF (ffmpeg).

ATRAC3plus: ffmpeg's native `atrac3plus` decoder is used (verified on the 51 DATA001
RIFF entries). It is not PPSSPP dependent. Fallback chain when ffmpeg or its decoder
is missing: `vgmstream-cli`, then Sony `at3tool` (WAV only). If none exists the call
raises AudioError with install instructions. OGG output uses ffmpeg's built-in
`vorbis` encoder (experimental, needs -strict -2) or `libvorbis` when present.
"""
import os
import shutil
import struct
import subprocess
import tempfile


class AudioError(RuntimeError):
    pass


# --- VAG -------------------------------------------------------------------

_COEF = ((0, 0), (60, 0), (115, -52), (98, -55), (122, -60))


def vag_decode(data):
    """Decode a VAGp/pBAV file. Returns (sample_rate, mono int16 samples as array('h')).

    Header (big endian, 0x30 bytes): 'VAGp', version, reserved, size, rate, ..., name[16].
    Frames are 16 bytes: shift/predict byte, flag byte, 14 bytes of nibbles (low first),
    28 samples each. Flag 1 or 7 ends the stream (the header size field is unreliable).
    """
    import array
    if data[:4] == b"VAGp":
        rate = struct.unpack_from(">I", data, 16)[0]
        pos = 0x30
    elif data[:4] == b"pBAV":
        rate = struct.unpack_from("<I", data, 16)[0]
        pos = 0x30
    else:
        raise AudioError("not a VAG file")
    if not 4000 <= rate <= 192000:
        raise AudioError("implausible VAG sample rate %d" % rate)
    out = array.array("h")
    h1 = h2 = 0
    n = len(data)
    first = True
    while pos + 16 <= n:
        sp, flag = data[pos], data[pos + 1]
        shift, pred = sp & 15, min(sp >> 4, 4)
        if flag in (1, 7) and not first:
            break
        first = False
        f0, f1 = _COEF[pred]
        blk = data[pos + 2:pos + 16]
        for b in blk:
            for nib in (b & 15, b >> 4):
                s = (nib - 16 if nib > 7 else nib) << 12
                s = (s >> shift) + ((h1 * f0 + h2 * f1) >> 6)
                s = -32768 if s < -32768 else 32767 if s > 32767 else s
                out.append(s)
                h2, h1 = h1, s
        pos += 16
    return rate, out


def vag_split(data):
    """Split a bank of concatenated VAGp files. Returns list of (offset, name, bytes).

    DATA001 entry 0 is such a bank (103 sounds). Each file's header size field gives
    the payload length; the next header is searched from there on 16-byte boundaries.
    """
    starts = []
    pos = 0
    n = len(data)
    while pos + 0x30 <= n:
        if data[pos:pos + 4] != b"VAGp":
            pos += 16
            continue
        starts.append(pos)
        size = struct.unpack_from(">I", data, pos + 12)[0]
        pos += 0x30 + max(size, 0)
        pos = (pos + 15) & ~15
    res = []
    for i, st in enumerate(starts):
        end = starts[i + 1] if i + 1 < len(starts) else n
        name = data[st + 0x20:st + 0x30].split(b"\0")[0].decode("ascii", "replace")
        res.append((st, name, data[st:end]))
    return res


def write_wav(path, rate, samples, channels=1):
    """Write 16-bit PCM WAV from an array('h') of interleaved samples."""
    import sys
    pcm = samples.tobytes() if sys.byteorder == "little" else bytes(
        b for s in samples for b in struct.pack("<h", s))
    hdr = b"RIFF" + struct.pack("<I", 36 + len(pcm)) + b"WAVEfmt " + struct.pack(
        "<IHHIIHH", 16, 1, channels, rate, rate * channels * 2, channels * 2, 16) + b"data" + struct.pack("<I", len(pcm))
    with open(path, "wb") as f:
        f.write(hdr + pcm)


# --- ffmpeg ----------------------------------------------------------------

def find_ffmpeg():
    return shutil.which("ffmpeg")


def ffmpeg_has_atrac3plus(ffmpeg=None):
    ffmpeg = ffmpeg or find_ffmpeg()
    if not ffmpeg:
        return False
    r = subprocess.run([ffmpeg, "-hide_banner", "-decoders"], capture_output=True, text=True)
    return " atrac3plus " in r.stdout


def _ogg_encoder_args(ffmpeg):
    r = subprocess.run([ffmpeg, "-hide_banner", "-encoders"], capture_output=True, text=True)
    if " libvorbis " in r.stdout:
        return ["-c:a", "libvorbis", "-q:a", "5"]
    if " vorbis " in r.stdout:
        # native encoder is stereo only: upmix mono
        return ["-c:a", "vorbis", "-strict", "-2", "-ac", "2", "-b:a", "160k"]
    raise AudioError("ffmpeg has no vorbis encoder; write WAV instead")


def _run_ffmpeg(src, dst, ogg):
    ffmpeg = find_ffmpeg()
    if not ffmpeg:
        raise AudioError("ffmpeg not found on PATH (brew install ffmpeg)")
    args = [ffmpeg, "-v", "error", "-y", "-i", src]
    args += _ogg_encoder_args(ffmpeg) if ogg else ["-c:a", "pcm_s16le"]
    r = subprocess.run(args + [dst], capture_output=True, text=True)
    if r.returncode != 0:
        raise AudioError("ffmpeg failed: " + r.stderr.strip()[:300])


def atrac_to_audio(src, dst):
    """ATRAC3plus RIFF (.at3) -> .wav or .ogg by dst extension."""
    ogg = dst.lower().endswith(".ogg")
    if find_ffmpeg() and ffmpeg_has_atrac3plus():
        return _run_ffmpeg(src, dst, ogg)
    for tool in ("vgmstream-cli",):
        exe = shutil.which(tool)
        if exe:
            wav = dst if not ogg else tempfile.mktemp(suffix=".wav")
            subprocess.run([exe, "-o", wav, src], check=True, capture_output=True)
            if ogg:
                _run_ffmpeg(wav, dst, True)
                os.unlink(wav)
            return
    exe = shutil.which("at3tool")
    if exe and not ogg:
        subprocess.run([exe, "-d", src, dst], check=True, capture_output=True)
        return
    raise AudioError("no ATRAC3plus decoder: need ffmpeg with the atrac3plus decoder, "
                     "or vgmstream-cli, or Sony at3tool (WAV only)")


def vag_to_audio(src, dst):
    with open(src, "rb") as f:
        rate, pcm = vag_decode(f.read())
    if not len(pcm):
        raise AudioError("VAG decoded to zero samples")
    if dst.lower().endswith(".ogg"):
        tmp = tempfile.mktemp(suffix=".wav")
        try:
            write_wav(tmp, rate, pcm)
            _run_ffmpeg(tmp, dst, True)
        finally:
            if os.path.exists(tmp):
                os.unlink(tmp)
    else:
        write_wav(dst, rate, pcm)


def detect_audio(path):
    with open(path, "rb") as f:
        h = f.read(12)
    if h[:4] in (b"VAGp", b"pBAV"):
        return "vag"
    if h[:4] == b"RIFF" and h[8:12] == b"WAVE":
        return "atrac"
    raise AudioError("unrecognised audio file: %s" % path)


def convert_audio(src, dst):
    """Convert one audio file to dst (.wav or .ogg). Returns the detected kind."""
    d = os.path.dirname(dst)
    if d:
        os.makedirs(d, exist_ok=True)
    kind = detect_audio(src)
    (vag_to_audio if kind == "vag" else atrac_to_audio)(src, dst)
    return kind
