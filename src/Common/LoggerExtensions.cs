using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace ArcanumLib.Common
{
    /// <summary>
    /// Logging and safe-execution helpers for Vintage Story APIs.
    /// Use these to avoid leaving bare try/catch blocks throughout a mod.
    /// </summary>
    public static class LoggerExtensions
    {
        /// <summary>
        /// Logs a non-critical warning with a context prefix.
        /// </summary>
        public static void LogNonCriticalWarning(this ICoreAPI api, string context, Exception ex)
        {
            api?.Logger?.Warning("[ArcanumLib] [{0}] non-critical operation failed: {1}", context, ex?.Message ?? "unknown");
        }

        /// <summary>
        /// Logs a client-side GUI warning with a context prefix.
        /// </summary>
        public static void LogGuiWarning(this ICoreClientAPI capi, string context, Exception ex)
        {
            capi?.Logger?.Warning("[ArcanumLib] [{0}] non-critical GUI operation failed: {1}", context, ex?.Message ?? "unknown");
        }

        /// <summary>
        /// Logs a server-side warning with a context prefix.
        /// </summary>
        public static void LogNonCriticalWarning(this ICoreServerAPI sapi, string context, Exception ex)
        {
            sapi?.Logger?.Warning("[ArcanumLib] [{0}] non-critical operation failed: {1}", context, ex?.Message ?? "unknown");
        }

        /// <summary>
        /// Executes the given action and logs any exception as a non-critical warning.
        /// </summary>
        public static void SafeExecute(this ICoreAPI api, string context, Action action)
        {
            if (action == null) return;
            try { action(); }
            catch (Exception ex) { api.LogNonCriticalWarning(context, ex); }
        }

        /// <summary>
        /// Executes the given action on the client and logs any exception as a GUI warning.
        /// </summary>
        public static void SafeExecute(this ICoreClientAPI capi, string context, Action action)
        {
            if (action == null) return;
            try { action(); }
            catch (Exception ex) { capi.LogGuiWarning(context, ex); }
        }

        /// <summary>
        /// Executes the given action on the server and logs any exception as a non-critical warning.
        /// </summary>
        public static void SafeExecute(this ICoreServerAPI sapi, string context, Action action)
        {
            if (action == null) return;
            try { action(); }
            catch (Exception ex) { sapi.LogNonCriticalWarning(context, ex); }
        }
    }
}
