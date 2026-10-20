"""Original Riverbank instrumental loops. Deterministic offline additive synthesis.
No sampled recordings, online music or third-party compositions are used.
Run with Python + numpy. Audio assets are generated into ../Audio.
"""
from pathlib import Path
import json
import wave
import numpy as np

RATE = 22050
OUT = Path(__file__).resolve().parents[1] / 'Audio'
OUT.mkdir(exist_ok=True)

# Pentatonic motifs and sparse open voicings keep the score gentle under gameplay.
SCORES = {
 'spring': (84, 62, [0,4,7,9,12,9,7,4,2,4,7,4,2,0,2,7], 'flute'),
 'summer': (96, 60, [7,9,12,14,12,9,7,4,7,9,7,4,2,4,0,4], 'pluck'),
 'autumn': (72, 57, [12,7,4,2,0,4,7,9,7,4,2,0,2,4,7,4], 'wood'),
 'winter': (64, 62, [12,7,2,7,9,7,4,2,7,4,2,0,2,7,9,7], 'bell'),
 'thunderstorm': (104, 50, [0,3,7,10,7,3,2,0,7,10,12,10,7,5,3,2], 'storm'),
 'snow': (60, 65, [12,9,7,4,7,2,4,7,12,14,12,9,7,4,2,7], 'snow'),
 'wind': (88, 57, [0,7,12,9,7,2,4,9,7,14,12,7,9,4,2,7], 'wind'),
}

def compose(name, bpm, root, motif, voice):
    rng = np.random.default_rng(1100 + list(SCORES).index(name))
    beat = 60 / bpm
    n = round(64 * beat * RATE)  # 16 bars, with wrapped release/reverb tails
    mix = np.zeros((n, 2), dtype=np.float64)

    def note(start, beats, midi, amp, kind, pan=0):
        duration = beats * beat
        t = np.arange(max(1, round(duration * RATE))) / RATE
        f = 440 * 2 ** ((midi - 69) / 12)
        attack = .07 if kind == 'pad' else .018
        env = np.minimum(1, t / attack) * np.minimum(1, (duration - t) / .22)
        phase = 2*np.pi*f*t
        if kind == 'pad':
            tone = (.55*np.sin(phase + .005*np.sin(2*np.pi*.8*t))
                    + .25*np.sin(phase*1.0017) + .12*np.sin(2*phase))
        elif kind in ('bell','snow'):
            tone = np.sin(phase)*np.exp(-t*1.5) + .28*np.sin(2*phase)*np.exp(-t*3) + .1*np.sin(3*phase)*np.exp(-t*5)
        elif kind == 'flute':
            tone = np.sin(phase+.03*np.sin(2*np.pi*4.7*t)) + .2*np.sin(2*phase) + .05*np.sin(3*phase)
            tone *= np.exp(-t*.65)
        else:
            tone = (np.sin(phase)+.32*np.sin(2*phase)+.14*np.sin(3*phase)) * np.exp(-t*(3 if kind=='wood' else 2.1))
        tone *= amp * np.maximum(0, env)
        at = round(start * beat * RATE)
        # Wrapped signals make the end/start seam part of the same continuous arrangement.
        for delay, gain, side in ((0,1,pan),(.19,.15,-pan),(.37,.08,pan)):
            idx = (at + round(delay*RATE) + np.arange(len(tone))) % n
            np.add.at(mix[:,0], idx, tone * gain * np.sqrt((1-side)*.5))
            np.add.at(mix[:,1], idx, tone * gain * np.sqrt((1+side)*.5))

    minor = name == 'thunderstorm'
    # I / vi / IV / V color; storm uses minor i / VI / iv / v.
    chords = [(0,3 if minor else 4,7),(8 if minor else 9,12,15 if minor else 16),
              (5,8 if minor else 9,12),(7,10 if minor else 11,14)]
    for bar in range(16):
        chord = chords[(bar//2)%4]
        for interval in chord:
            note(bar*4, 4.5, root-12+interval, .10, 'pad', (interval%3-1)*.3)
        note(bar*4, 3.8, root-24+chord[0], .1, 'pad')
        for k in range(4):
            note(bar*4+k, 1.7, root+chord[(bar+k)%3], .065, 'wood' if voice=='wood' else 'pluck', (-1 if k%2 else 1)*.45)
        for k in range(2):
            step = motif[(bar*2+k)%16]
            note(bar*4+k*2+.12, 1.8, root+step+(12 if voice=='snow' else 0), .14, voice, (-1 if bar%2 else 1)*.2)
        if voice in ('storm','wind'):
            note(bar*4+1.5, .6, root+19, .04, 'pluck', .6)

    # Restrained synthetic event ambience, beneath an actual instrumental score.
    if voice in ('storm','wind','snow'):
        white = rng.normal(0,1,n)
        cutoff = 450 if voice=='wind' else 2400
        spec = np.fft.rfft(white)
        hz = np.fft.rfftfreq(n, 1/RATE)
        spec *= np.exp(-(hz/cutoff)**2) * (1-np.exp(-(hz/70)**2))
        air = np.fft.irfft(spec,n=n)
        t = np.arange(n)/n
        air *= (.5+.5*np.sin(2*np.pi*t*4)) * (.012 if voice=='snow' else .04)
        mix[:,0] += air; mix[:,1] += np.roll(air, round(.023*RATE))
        if voice=='storm':
            for at in (14,46):
                duration = 4*beat
                t = np.arange(round(duration*RATE))/RATE
                rumble = (np.sin(2*np.pi*42*t)+.5*np.sin(2*np.pi*67*t)) * np.minimum(1,t/.35)*np.exp(-t*1.8)*.1
                idx = (round(at*beat*RATE)+np.arange(len(t)))%n
                mix[idx,0]+=rumble;mix[idx,1]+=rumble*.9
    mix -= mix.mean(axis=0)
    # Common loudness, conservative true-peak margin; no hard limiting.
    rms = np.sqrt(np.mean(mix**2))
    mix *= min(.13/max(rms,1e-9), .72/max(np.max(np.abs(mix)),1e-9))
    pcm = np.round(np.clip(mix,-.99,.99)*32767).astype('<i2')
    with wave.open(str(OUT/(name+'.wav')), 'wb') as out:
        out.setnchannels(2);out.setsampwidth(2);out.setframerate(RATE);out.writeframes(pcm.tobytes())
    return dict(track=name, seconds=round(n/RATE,3), bpm=bpm,
                rms=round(float(np.sqrt(np.mean(mix**2))),4), peak=round(float(np.max(np.abs(mix))),4),
                seam=round(float(np.max(np.abs(mix[0]-mix[-1]))),4))

if __name__ == '__main__':
    reports = [compose(name,*score) for name,score in SCORES.items()]
    (OUT/'manifest.json').write_text(json.dumps(reports,indent=2),encoding='utf-8')
    print(json.dumps(reports,indent=2))
