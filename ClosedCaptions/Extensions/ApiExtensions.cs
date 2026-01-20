using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.Client.NoObf;

namespace ClosedCaptions.Extensions;

public static class ApiExtensions
{
	public static ClientMain AsClientMain(this ICoreClientAPI api)
	{
		return (ClientMain)api.World;
	}

	public static Queue<ILoadedSound> GetActiveSounds(this ICoreClientAPI api)
	{
		var clientMain = api.AsClientMain();
		
		// 146
		var activeSoundsField = clientMain.GetType().GetField("ActiveSounds",
			System.Reflection.BindingFlags.Instance |
			System.Reflection.BindingFlags.DeclaredOnly |
			System.Reflection.BindingFlags.NonPublic);
		var activeSounds = (Queue<ILoadedSound>)activeSoundsField?.GetValue(clientMain)!;

		return activeSounds;
	}
}