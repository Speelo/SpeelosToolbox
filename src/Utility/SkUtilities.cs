using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace SkToolbox.Utility
{
    public static class SkUtilities
    {
        public static bool ConvertInternalWarningsErrors = false; // Should we allow output of warnings and errors from SkToolbox, or suppress them all to regular log output? // True = suppress
        public static BindingFlags BindFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

        /// <summary>
        /// Uses reflection to get the field value from an object.
        /// </summary>
        ///
        /// <param name="type">The instance type.</param>
        /// <param name="instance">The instance object.</param>
        /// <param name="fieldName">The field's name which is to be fetched.</param>
        ///
        /// <returns>The field value from the object.</returns>
        //internal static object GetInstanceField(System.Type type, object instance, string fieldName)
        //{
        //    FieldInfo field = type.GetField(fieldName, BindFlags);
        //    return field.GetValue(instance);
        //}

        //internal static object SetInstanceField(System.Type type, object instance, string fieldName, object fieldValue)
        //{
        //    FieldInfo field = type.GetField(fieldName, BindFlags);
        //    field.SetValue(instance, fieldValue);

        //    return field.GetValue(instance);
        //}

        // Valheim 1.0 moved several members SkToolbox pokes (m_chatBuffer, commands, m_history,
        // m_terminalInstance...) from Console up into the Terminal base class as private members.
        // Type.GetField/GetProperty/GetMethod never return a base class's *private* members, so the
        // lookups below walk the hierarchy explicitly. A missing member is logged once instead of
        // throwing NullReferenceException deep inside a command.
        private static readonly System.Collections.Generic.HashSet<string> reportedMissing = new System.Collections.Generic.HashSet<string>();

        private static void ReportMissing(string kind, Type type, string name)
        {
            string key = kind + ":" + type.FullName + "." + name;
            if (reportedMissing.Add(key))
            {
                Logz(new string[] { "REFLECTION" }, new string[] { kind + " '" + name + "' not found on " + type.FullName + " (renamed in this Valheim version?)" }, LogType.Error);
            }
        }

        public static FieldInfo FindField(Type type, string fieldName)
        {
            for (Type t = type; t != null; t = t.BaseType)
            {
                FieldInfo f = t.GetField(fieldName, BindFlags | BindingFlags.DeclaredOnly);
                if (f != null) return f;
            }
            return null;
        }

        public static PropertyInfo FindProperty(Type type, string propertyName)
        {
            for (Type t = type; t != null; t = t.BaseType)
            {
                PropertyInfo p = t.GetProperty(propertyName, BindFlags | BindingFlags.DeclaredOnly);
                if (p != null) return p;
            }
            return null;
        }

        public static MethodInfo FindMethod(Type type, string methodName)
        {
            for (Type t = type; t != null; t = t.BaseType)
            {
                MethodInfo m = t.GetMethod(methodName, BindFlags | BindingFlags.DeclaredOnly);
                if (m != null) return m;
            }
            return null;
        }

        public static void SetPrivateField(this object obj, string fieldName, object value)
        {
            FieldInfo field = FindField(obj.GetType(), fieldName);
            if (field == null) { ReportMissing("Field", obj.GetType(), fieldName); return; }
            field.SetValue(obj, value);
        }

        public static T GetPrivateField<T>(this object obj, string fieldName)
        {
            FieldInfo field = FindField(obj.GetType(), fieldName);
            if (field == null) { ReportMissing("Field", obj.GetType(), fieldName); return default(T); }
            object value = field.GetValue(obj);
            return value is T ? (T)value : default(T);
        }

        public static void SetPrivateProperty(this object obj, string propertyName, object value)
        {
            PropertyInfo prop = FindProperty(obj.GetType(), propertyName);
            if (prop == null) { ReportMissing("Property", obj.GetType(), propertyName); return; }
            prop.SetValue(obj, value, null);
        }

        public static T GetPrivateProperty<T>(this object obj, string propertyName)
        {
            PropertyInfo prop = FindProperty(obj.GetType(), propertyName);
            if (prop == null) { ReportMissing("Property", obj.GetType(), propertyName); return default(T); }
            object value = prop.GetValue(obj, null);
            return value is T ? (T)value : default(T);
        }

        public static void InvokePrivateMethod(this object obj, string methodName, object[] methodParams)
        {
            MethodInfo dynMethod = FindMethod(obj.GetType(), methodName);
            if (dynMethod == null) { ReportMissing("Method", obj.GetType(), methodName); return; }
            dynMethod.Invoke(obj, methodParams);
        }

        public static Component CopyComponent(Component original, Type originalType, Type overridingType,
            GameObject destination)
        {
            var copy = destination.AddComponent(overridingType);
            var fields = originalType.GetFields(BindFlags);
            foreach (var field in fields)
            {
                var value = field.GetValue(original);
                field.SetValue(copy, value);
            }

            return copy;
        }

        public enum Status
        {
            Initialized,
            Loading,
            Ready,
            Error,
            Unload
        }

        /// <summary>
        /// Used for logging to the console in a controlled manner<br>Example Usage: SkUtilities.Logz(new string[] { "CMD", "REQ" }, new string[] { "Submenu Created" });</br>
        /// </summary>
        /// <param name="categories"></param>
        /// <param name="messages"></param>
        /// <param name="callerClass"></param>
        /// <param name="callerMethod"></param>
        public static void Logz(string[] categories, string[] messages, LogType logType = LogType.Log)
        {
            string strBuild = string.Empty;
            if (categories != null)
            {
                foreach (string cat in categories)
                {
                    strBuild = strBuild + " (" + cat + ") -> ";
                }
            }
            if (messages != null)
            {
                foreach (string msg in messages)
                {
                    if (msg != null)
                    {
                        strBuild = strBuild + msg + " | ";
                    }
                    else
                    {
                        strBuild = strBuild + "NULL" + " | ";
                    }
                }
                strBuild = strBuild.Remove(strBuild.Length - 2, 1);
            }
            //Get the class that called the log
            if (!ConvertInternalWarningsErrors)
            {
                switch (logType)
                {
                    case LogType.Error:
                        Debug.LogError("(Speelo's Menu) -> " + strBuild);
                        break;
                    case LogType.Warning:
                        Debug.LogWarning("(Speelo's Menu) -> " + strBuild);
                        break;
                    default:
                        Debug.Log("(Speelo's Menu) -> " + strBuild);
                        break;
                }
            }
            else
            {
                Debug.Log("(Speelo's Menu) -> " + strBuild);
            }
        }

        public static string Logr(string[] categories, string[] messages)
        {
            string strBuild = string.Empty;
            if (categories != null)
            {
                foreach (string cat in categories)
                {
                    strBuild = strBuild + " (" + cat + ") -> ";
                }
            }
            if (messages != null)
            {
                foreach (string msg in messages)
                {
                    if (msg != null)
                    {
                        strBuild = strBuild + msg + " | ";
                    }
                    else
                    {
                        strBuild = strBuild + "NULL" + " | ";
                    }
                }
                strBuild = strBuild.Remove(strBuild.Length - 2, 1);
            }
            return "(Speelo's Menu) -> " + strBuild;
        }

        /// <summary>
        /// Used for logging to the console in a controlled manner
        /// </summary>
        /// <param name="message"></param>
        /// <param name="callerClass"></param>
        /// <param name="callerMethod"></param>
        public static void Logz(string message)
        {
            string strBuild = string.Empty;

            strBuild += " (OUT) -> ";
            strBuild = $"{strBuild}{message} ";

            Debug.Log("(Speelo's Menu) -> " + strBuild);
        }

        // GUI Items
        public static void RectFilled(float x, float y, float width, float height, Texture2D text)
        {
            GUI.DrawTexture(new Rect(x, y, width, height), text);
        }

        public static void RectOutlined(float x, float y, float width, float height, Texture2D text, float thickness = 1f)
        {
            RectFilled(x, y, thickness, height, text);
            RectFilled(x + width - thickness, y, thickness, height, text);
            RectFilled(x + thickness, y, width - thickness * 2f, thickness, text);
            RectFilled(x + thickness, y + height - thickness, width - thickness * 2f, thickness, text);
        }
    }
}

