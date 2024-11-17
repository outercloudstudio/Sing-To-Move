using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class Player : CharacterBody3D
{
	private float Speed = 3f;
	private float TurnSpeed = 60f;

	private AudioEffectCapture _captureEffect;
	private Dictionary<string, float> _notes = new Dictionary<string, float>()
	{
		{"C", 261.63f},
		{"D", 293.66f},
		{"E", 329.63f},
		{"F", 349.23f},
		{"G", 392.00f},
		{"A", 440.00f},
		{"B", 493.88f},
	};

	private string _currentNote;

	public override void _Ready()
	{
		int busIndex = AudioServer.GetBusIndex("Record");
		_captureEffect = (AudioEffectCapture)AudioServer.GetBusEffect(busIndex, 0);
	}

	public override void _Process(double delta)
	{
		GetNote();
	}

	public override void _PhysicsProcess(double delta)
	{
		Velocity = Vector3.Zero;

		if (_currentNote == "A" || _currentNote == "G")
		{
			RotateY(Mathf.DegToRad(TurnSpeed) * (float)delta);
		}
		else if (_currentNote == "C" || _currentNote == "D")
		{
			Velocity = -GlobalBasis.Z * Speed;
		}
		else if (_currentNote == "E" || _currentNote == "F")
		{
			RotateY(Mathf.DegToRad(-TurnSpeed) * (float)delta);
		}

		MoveAndSlide();
	}

	private void GetNote()
	{
		if (_captureEffect.GetFramesAvailable() < 4096) return;

		Vector2[] frames = _captureEffect.GetBuffer(4096);
		double[] signal = frames.Select(frame => (double)frame.X).ToArray();

		GD.Print("Length: " + signal.Length);

		var window = new FftSharp.Windows.Hanning();
		window.ApplyInPlace(signal);

		System.Numerics.Complex[] spectrum = FftSharp.FFT.Forward(signal);
		Dictionary<float, float> frequencies = new Dictionary<float, float>();

		for (int index = 0; index < spectrum.Length; index++)
		{
			frequencies.Add(index * AudioServer.GetMixRate() / signal.Length, (float)spectrum[index].Magnitude);
			// GD.Print($"{(index * AudioServer.GetMixRate() / signal.Length).ToString().PadRight(20, ' ')} => {new String('#', Mathf.FloorToInt((float)spectrum[index].Magnitude) / 2)}");
		}

		KeyValuePair<float, float> highestFrequency = frequencies.MaxBy(pair => pair.Value);

		string closestNote = "C";
		float closestFrequency = 261.63f;

		for (int octave = -2; octave <= 2; octave++)
		{
			foreach (KeyValuePair<string, float> note in _notes)
			{
				if (Math.Abs(highestFrequency.Key - closestFrequency) < Mathf.Abs(highestFrequency.Key - (note.Value * Mathf.Pow(2f, octave)))) continue;

				closestFrequency = note.Value * Mathf.Pow(2f, octave);
				closestNote = note.Key;
			}
		}

		GD.Print("Note: " + closestNote + " " + highestFrequency.Key + " " + highestFrequency.Value);

		_captureEffect.ClearBuffer();

		_currentNote = closestNote;

		if (highestFrequency.Value < 4f) _currentNote = null;
	}
}
