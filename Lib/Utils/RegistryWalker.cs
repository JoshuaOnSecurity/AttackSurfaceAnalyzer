﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using AttackSurfaceAnalyzer.Objects;
using Microsoft.Win32;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;

namespace AttackSurfaceAnalyzer.Utils
{
    public static class RegistryWalker
    {
        public static IEnumerable<RegistryKey> WalkHive(RegistryHive Hive, string startingKey = null)
        {
            Stack<RegistryKey> keys = new Stack<RegistryKey>();

            RegistryKey x86_View = null, x64_View = null;
            try
            {
                x86_View = RegistryKey.OpenBaseKey(Hive, RegistryView.Registry32);
            }
            catch (Exception e) when (
                e is IOException ||
                e is ArgumentException ||
                e is UnauthorizedAccessException ||
                e is System.Security.SecurityException)
            {

            }
            try
            {
                x64_View = RegistryKey.OpenBaseKey(Hive, RegistryView.Registry64);
            }
            catch (Exception e) when (
                e is IOException ||
                e is ArgumentException ||
                e is UnauthorizedAccessException ||
                e is System.Security.SecurityException)
            {

            }

            if (x86_View != null)
            {
                if (startingKey != null)
                {
                    x86_View = x86_View.OpenSubKey(startingKey, writable: false);
                }
                keys.Push(x86_View);
            }

            if (x64_View != null)
            {
                if (startingKey != null)
                {
                    x64_View = x64_View.OpenSubKey(startingKey, writable: false);
                }
                keys.Push(x64_View);
            }

            while (keys.Count > 0)
            {
                RegistryKey currentKey = keys.Pop();

                if (currentKey == null)
                {
                    continue;
                }
                if (Filter.IsFiltered(AsaHelpers.GetPlatformString(), "Scan", "Registry", "Key", currentKey.Name))
                {
                    continue;
                }

                // First push all the new subkeys onto our stack.
                foreach (string key in currentKey.GetSubKeyNames())
                {
                    try
                    {
                        var next = currentKey.OpenSubKey(name: key, writable: false);
                        keys.Push(next);
                    }
                    // These are expected as we are running as administrator, not System.
                    catch (System.Security.SecurityException)
                    {
                        Log.Debug("Permission Denied Opening Subkey: {0}\\{1}", currentKey.Name, key);
                    }
                    // There seem to be some keys which are listed as existing by the APIs but don't actually exist.
                    // Unclear if these are just super transient keys or what the other cause might be.
                    // Since this isn't user actionable, also just supress these to the verbose stream.
                    catch (System.IO.IOException)
                    {
                        Log.Debug("IOError Reading: {0}\\{1}", currentKey.Name, key);
                    }
                    catch (Exception e)
                    {
                        Log.Information(e, "Unexpected error when parsing {0}\\{1}", currentKey.Name, key);
                        AsaTelemetry.TrackTrace(Microsoft.ApplicationInsights.DataContracts.SeverityLevel.Error, e);
                    }
                }

                yield return currentKey;
            }

            x86_View.Dispose();
            x64_View.Dispose();
        }

        public static RegistryObject RegistryKeyToRegistryObject(RegistryKey registryKey)
        {
            RegistryObject regObj = null;
            if (registryKey == null) { return regObj; }

            regObj = new RegistryObject()
            {
                Key = registryKey.Name,
            };
            try
            {
                regObj.AddSubKeys(registryKey.GetSubKeyNames());
            }
            catch (System.ArgumentException)
            {
                Log.Debug("Invalid Handle (ArgumentException) {0}", registryKey.Name);
            }
            catch (Exception e)
            {
                Log.Debug(e, "Couldn't process reg key {0}", registryKey.Name);
            }

            try
            {
                foreach (RegistryAccessRule rule in registryKey.GetAccessControl().GetAccessRules(true, true, typeof(System.Security.Principal.SecurityIdentifier)))
                {
                    string name = rule.IdentityReference.Value;

                    try
                    {
                        name = rule.IdentityReference.Translate(typeof(NTAccount)).Value;
                    }
                    catch (IdentityNotMappedException)
                    {
                        // This is fine. Some SIDs don't map to NT Accounts.
                    }

                    if (regObj.Permissions.ContainsKey(name))
                    {
                        regObj.Permissions[name].Add(rule.RegistryRights.ToString());
                    }
                    else
                    {
                        regObj.Permissions.Add(name, new List<string>() { rule.RegistryRights.ToString() });
                    }
                }
            }
            catch (ArgumentException)
            {
                Log.Debug("Failed to get permissions (handle is invalid) for {0}", regObj.Key);
            }
            catch (Exception e)
            {
                Log.Debug(e, "Failed to get permissions for {0}", regObj.Key);
            }


            foreach (string valueName in registryKey.GetValueNames())
            {
                try
                {
                    regObj.Values.Add(valueName, (registryKey.GetValue(valueName) == null) ? "" : (registryKey.GetValue(valueName).ToString()));
                }
                catch (Exception ex)
                {
                    Log.Debug(ex, "Found an exception processing registry values of {0}.", registryKey.Name);
                }
            }


            return regObj;
        }
    }
}