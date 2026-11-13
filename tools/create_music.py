"""Compose an original 40-second seamless, gentle pixel-adventure music loop."""
from pathlib import Path
import wave
import numpy as np

RATE=44100
BEAT=.625
DURATION=BEAT*64
score=np.zeros((round(RATE*DURATION),2),dtype=np.float64)

def note(midi,beat,length,gain,voice="pluck",pan=0):
    start=round(beat*BEAT*RATE)
    count=round(length*BEAT*RATE)
    t=np.arange(count)/RATE
    hz=440*2**((midi-69)/12)
    sine=np.sin(2*np.pi*hz*t)
    if voice=="bell":
        signal=sine+.3*np.sin(2*np.pi*hz*2*t)+.1*np.sin(2*np.pi*hz*3*t)
        envelope=(1-np.exp(-t*140))*np.exp(-t*4)
    elif voice=="pad":
        signal=sine+.12*np.sin(2*np.pi*hz*2*t)
        envelope=np.minimum(t/.08,1)*np.minimum(np.maximum(t[-1]-t,0)/.14,1)
    else:
        signal=sine+.22*np.sin(2*np.pi*hz*3*t)+.07*np.sin(2*np.pi*hz*5*t)
        envelope=(1-np.exp(-t*110))*np.exp(-t*2.8)*np.minimum(np.maximum(t[-1]-t,0)/.035,1)
    signal=signal*envelope*gain
    weights=np.sqrt(np.array([(1-pan)/2,(1+pan)/2]))
    indexes=(start+np.arange(count))%len(score)
    score[indexes]+=signal[:,None]*weights

# D major, B minor, G major and A major, with a second melodic phrase.
chords=[(50,62,66,69),(47,59,62,66),(43,55,59,62),(45,57,61,64)]*4
melodies=[
    [74,78,81,78,76,74,73,69], [74,73,71,66,69,71,74,78],
    [79,78,74,71,74,78,79,83], [81,76,73,69,73,76,78,76],
    [74,78,81,86,83,81,78,74], [78,74,71,69,66,69,71,74],
    [79,83,86,83,81,79,78,74], [76,78,81,78,76,73,69,73],
]
for bar,chord in enumerate(chords):
    base=bar*4
    for pitch in chord[1:]:note(pitch,base,3.9,.048,"pad",-.3 if pitch%2 else .3)
    for beat in [0,2]:note(chord[0],base+beat,1.6,.15,"pluck")
    arp=[chord[1]+12,chord[2]+12,chord[3]+12,chord[2]+12]
    for step in range(8):note(arp[step%4],base+step*.5,.7,.034,"bell",(-1 if step%2 else 1)*.45)
    melody=melodies[bar%8]
    for step,pitch in enumerate(melody):note(pitch,base+step*.5,.58,.075,"pluck",.12)

# A circular delay preserves the loop seam without a hard stop.
score+=np.roll(score,round(RATE*.1875),axis=0)*.17
score+=np.roll(score,round(RATE*.375),axis=0)*.09
score=np.tanh(score*1.5)
score*=.76/max(np.max(np.abs(score)),.001)
out=Path(__file__).resolve().parents[1]/"FINALS_SANDBOX/Assets/Sandbox/Audio/Mossmere-Meadow.wav"
out.parent.mkdir(parents=True,exist_ok=True)
with wave.open(str(out),"wb") as wav:
    wav.setnchannels(2);wav.setsampwidth(2);wav.setframerate(RATE)
    wav.writeframes((score*32767).astype("<i2").tobytes())
print(f"Created original music: {out.name}, {DURATION:.1f}s stereo, peak={np.max(np.abs(score)):.2f}")
