using UnityEngine;
using FMODUnity;

public static class AudioHelper
{

	public static void PlayOneShotWithParameters(string fmodEvent, Vector3 position, params (string name, float value)[] parameters)
	{
		FMOD.Studio.EventInstance instance = FMODUnity.RuntimeManager.CreateInstance(fmodEvent);

		foreach (var (name, value) in parameters)
		{
			instance.setParameterByName(name, value);
		}

		instance.set3DAttributes(position.To3DAttributes());
		instance.start();
		instance.release();
	}

	public static void PlayOneShotWithParameters(EventReference reference, Vector3 position, params (string name, float value)[] parameters)
	{
		FMOD.Studio.EventInstance instance = FMODUnity.RuntimeManager.CreateInstance(reference);

		foreach (var (name, value) in parameters)
		{
			instance.setParameterByName(name, value);
		}

		instance.set3DAttributes(position.To3DAttributes());
		instance.start();
		instance.release();
	}
}