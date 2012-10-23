using System;
using System.Collections;
using System.Text;
using System.IO;
using Microsoft.SPOT;
using System.Threading;

namespace Controller
{
	/// <summary>
	/// This class encodes and decodes JSON strings.
	/// Spec. details, see http://www.json.org/
	///
	/// JSON uses Arrays and Objects. These correspond here to the datatypes ArrayList and Hashtable.
	/// All numbers are parsed to doubles.
	/// Pulled from http://techblog.procurios.nl/k/618/news/view/14605/14863/How-do-I-write-my-own-parser-for-JSON.html
    ///
    /// TestMonkey107
    /// Combined code from the two sources above
    /// Added the ability to decode from and serialize to a Filestream
    /// Added a few more data types
    /// Added support for FfDB datatypes
    ///
    ///
    ///
    ///
    ///
    /// </summary>
	/// 	
    public class JSON
	{
        public static bool AutoCasting = true;
        public const int TOKEN_NONE = 0;
		public const int TOKEN_CURLY_OPEN = 1;
		public const int TOKEN_CURLY_CLOSE = 2;
		public const int TOKEN_SQUARED_OPEN = 3;
		public const int TOKEN_SQUARED_CLOSE = 4;
		public const int TOKEN_COLON = 5;
		public const int TOKEN_COMMA = 6;
		public const int TOKEN_STRING = 7;
		public const int TOKEN_NUMBER = 8;
		public const int TOKEN_TRUE = 9;
		public const int TOKEN_FALSE = 10;
		public const int TOKEN_NULL = 11;

		private const int BUILDER_CAPACITY = 1000;

        /// <summary>
		/// Parses the string json into a value
		/// </summary>
		/// <param name="json">A JSON string.</param>
		/// <returns>An ArrayList, a Hashtable, a double, a string, null or bool</returns>
		public static object JsonDecode(ref string json)
		{
            bool success = false;
            return JsonDecode(ref json, ref success);
		}

        /// <summary>
        /// Parses the string json into a value; and fills 'success' with the successfullness of the parse.
        /// </summary>
        /// <param name="json">A JSON string.</param>
        /// <param name="success">Successful parse?</param>
		/// <returns>An ArrayList, a Hashtable, a double, a string, null or bool</returns>
        public static object JsonDecode(ref string json, ref bool success)
        {
            return JsonDecode(ref json, ref success, null);
        }
            
		/// <summary>
		/// Parses the string json into a value; and fills 'success' with the successfullness of the parse.
		/// </summary>
		/// <param name="json">A JSON string.</param>
		/// <param name="success">Successful parse?</param>
        /// <returns>An ArrayList, a Hashtable, a double, a string, null, bool, or custom object</returns>
        public static object JsonDecode(ref string json, ref bool success, JsonCustomObjectsDefinition jco)
		{
            success = false;
			if (json != null) {
                if (jco == null) jco = new JsonCustomObjectsDefinition();
                char[] charArray = json.ToCharArray();
                //int index = json.IndexOf('[');
                int index = 0;
                json = null;
                object value = ParseValue(charArray, ref index, ref success, ref jco);
                success = true;
                return value;
			} else {
				return null;
			}
		}


        /// <summary>
        /// Parses the Filestream json into a value
        /// </summary>
        /// <param name="jfs">A JSON Filestream.</param>
        /// <returns>An ArrayList, a Hashtable, a double, a string, null, true, or false</returns>
        public static object JsonDecode(FileStream jsonStream)
        {
            bool success = false;
            return JsonDecode(jsonStream, ref success);
        }

        /// <summary>
        /// Parses the FileStream json into a value; and fills 'success' with the successfullness of the parse.
        /// </summary>
        /// <param name="jfs">A JSON FileStream.</param>
        /// <param name="success">Successful parse?</param>
        /// <returns>An ArrayList, a Hashtable, a double, a string, null, true, or false</returns>
        public static object JsonDecode(FileStream jsonStream, ref bool success)
        {
            return JsonDecode(jsonStream, ref success, null);
        }
        
        /// <summary>
        /// Parses the FileStream json into a value; and fills 'success' with the successfullness of the parse.
        /// </summary>
        /// <param name="jfs">A JSON FileStream.</param>
        /// <param name="success">Successful parse?</param>
        /// <returns>An ArrayList, a Hashtable, a double, a string, null, true, or false</returns>
        public static object JsonDecode(FileStream jsonStream, ref bool success, JsonCustomObjectsDefinition jco)
        {
            success = false;
            if (jsonStream != null)
            {
                if (jco == null) jco = new JsonCustomObjectsDefinition();
                object value = ParseValue(jsonStream, ref success, ref jco);
                return value;
            }
            else
            {
                return null;
            }
        }

		/// <summary>
		/// Converts a Hashtable / ArrayList object into a JSON string
		/// </summary>
		/// <param name="json">A Hashtable / ArrayList</param>
		/// <returns>A JSON encoded string, or null if object 'json' is not serializable</returns>
		public static string JsonEncode(object json)
		{
			StringBuilder builder = new StringBuilder(BUILDER_CAPACITY);
			bool success = SerializeValue(json, builder);
			return (success ? builder.ToString() : null);
		}

        protected static object ParseObject(char[] json, ref int index, ref bool success, ref JsonCustomObjectsDefinition jco)
		{
            //Hashtable table = new Hashtable();
            object table = jco.HashTable.NewHashTable();
            int token;

			// {
			NextToken(json, ref index);

			bool done = false;
			while (!done) {
				token = LookAhead(json, index);
				if (token == JSON.TOKEN_NONE) {
					success = false;
					return null;
				} else if (token == JSON.TOKEN_COMMA) {
					NextToken(json, ref index);
				} else if (token == JSON.TOKEN_CURLY_CLOSE) {
					NextToken(json, ref index);
					return table;
				} else {

					// name
                    string name = ParseString(json, ref index, ref success);
                    if (!success)
                    {
						success = false;
						return null;
					}

					// :
					token = NextToken(json, ref index);
					if (token != JSON.TOKEN_COLON) {
						success = false;
						return null;
					}

					// value
                    object value = ParseValue(json, ref index, ref success, ref jco);
					if (!success) {
						success = false;
						return null;
					}

                    jco.HashTable.Set(table, name, value);
                    //HashTableSet(table, name, value);
                    //table[name] = value;
				}
			}

			return table;
		}

        protected static object ParseObject(FileStream jsonStream, ref bool success, ref JsonCustomObjectsDefinition jco)
        {
            //Hashtable table = new Hashtable();
            object table = jco.HashTable.NewHashTable();
            int token;

            // {
            NextToken(jsonStream);

            bool done = false;
            while (!done)
            {
                token = LookAhead(jsonStream);
                if (token == JSON.TOKEN_NONE)
                {
                    success = false;
                    return null;
                }
                else if (token == JSON.TOKEN_COMMA)
                {
                    NextToken(jsonStream);
                }
                else if (token == JSON.TOKEN_CURLY_CLOSE)
                {
                    NextToken(jsonStream);
                    return table;
                }
                else
                {

                    // name
                    
                    string name = ParseString(jsonStream, ref success);
                    if (!success)
                    {
                        success = false;
                        return null;
                    }

                    // :
                    token = NextToken(jsonStream);
                    if (token != JSON.TOKEN_COLON)
                    {
                        success = false;
                        return null;
                    }

                    // value
                    object value = ParseValue(jsonStream, ref success, ref jco);
                    if (!success)
                    {
                        success = false;
                        return null;
                    }

                    jco.HashTable.Set(table, name, value);
                    //HashTableSet(table,name,value);
                    //table[name] = value;
                    
                }
            }

            return table;
        }


        protected static object ParseArray(char[] json, ref int index, ref bool success, ref JsonCustomObjectsDefinition jco)
		{
			//ArrayList array = new ArrayList();
            object array = jco.ArrayList.NewArrayList();
			// [
			NextToken(json, ref index);

			bool done = false;
			while (!done) {
				int token = LookAhead(json, index);
				if (token == JSON.TOKEN_NONE) {
					success = false;
					return null;
				} else if (token == JSON.TOKEN_COMMA) {
					NextToken(json, ref index);
				} else if (token == JSON.TOKEN_SQUARED_CLOSE) {
					NextToken(json, ref index);
					break;
				} else {
                    object value = ParseValue(json, ref index, ref success, ref jco);
					if (!success) {
						return null;
					}
                    jco.ArrayList.Add(array, value);
                    //ArrayListAdd(array, value);
					//array.Add(value);
				}
			}

			return array;
		}

        protected static object ParseArray(FileStream jsonStream, ref bool success, ref JsonCustomObjectsDefinition jco)
        {
            //ArrayList array = new ArrayList();
            object array = jco.ArrayList.NewArrayList();
            // [
            NextToken(jsonStream);

            bool done = false;
            while (!done)
            {
                int token = LookAhead(jsonStream);
                if (token == JSON.TOKEN_NONE)
                {
                    success = false;
                    return null;
                }
                else if (token == JSON.TOKEN_COMMA)
                {
                    NextToken(jsonStream);
                }
                else if (token == JSON.TOKEN_SQUARED_CLOSE)
                {
                    NextToken(jsonStream);
                    break;
                }
                else
                {
                    object value = ParseValue(jsonStream, ref success, ref jco);
                    if (!success)
                    {
                        return null;
                    }
                    jco.ArrayList.Add(array, value);
                    //ArrayListAdd((object)array, (object)value);
                    //array.Add(value);
                }
            }

            return array;
        }


        protected static object ParseValue(char[] json, ref int index, ref bool success, ref JsonCustomObjectsDefinition jco)
		{
            Thread.Sleep(1);//helps the netduino handle interrupt requests 
            if (index < 0) index = 0;
            switch (LookAhead(json, index)) {
				case JSON.TOKEN_STRING:
                    return ParseString(json, ref index, ref success, ref jco);
				case JSON.TOKEN_NUMBER:
                    return ParseNumber(json, ref index, ref success, ref jco);
				case JSON.TOKEN_CURLY_OPEN:
                    return ParseObject(json, ref index, ref success, ref jco);
				case JSON.TOKEN_SQUARED_OPEN:
                    return ParseArray(json, ref index, ref success, ref jco);
                case JSON.TOKEN_TRUE:
                    NextToken(json, ref index);
                    return jco.BoolProperty.NewBool(true);
                case JSON.TOKEN_FALSE:
                    NextToken(json, ref index);
                    return jco.BoolProperty.NewBool(false);
                case JSON.TOKEN_NULL:
					NextToken(json, ref index);
					return null;
				case JSON.TOKEN_NONE:
					break;
			}

			success = false;
			return null;
		}


        protected static object ParseValue(FileStream jsonStream, ref bool success, ref JsonCustomObjectsDefinition jco)
        {
            Debug.Print("Memory: " + Debug.GC(true).ToString());
            Thread.Sleep(1);//helps the netduino handle interrupt requests 
            if (jsonStream != null)
            {
                switch (LookAhead(jsonStream))
                {
                    case JSON.TOKEN_STRING:
                        return ParseString(jsonStream, ref success, ref jco);
                    case JSON.TOKEN_NUMBER:
                        return ParseNumber(jsonStream, ref success, ref jco);
                    case JSON.TOKEN_CURLY_OPEN:
                        return ParseObject(jsonStream, ref success, ref jco);
                    case JSON.TOKEN_SQUARED_OPEN:
                        return ParseArray(jsonStream, ref success, ref jco);
                    case JSON.TOKEN_TRUE:
                        NextToken(jsonStream);
                        return jco.BoolProperty.NewBool(true);
                    case JSON.TOKEN_FALSE:
                        NextToken(jsonStream);
                        return jco.BoolProperty.NewBool(false);
                    case JSON.TOKEN_NULL:
                        NextToken(jsonStream);
                        return null;
                    case JSON.TOKEN_NONE:
                        break;
                }
            }
            success = false;
            return null;
        }

        protected static object ParseString(char[] json, ref int index, ref bool success, ref JsonCustomObjectsDefinition jco)
        {
            return jco.StringProperty.NewString(ParseString(json, ref index,ref success));
        }
        protected static string ParseString(char[] json, ref int index, ref bool success)
        {
            String s = "";
            char c;

			EatWhitespace(json, ref index);

			// "
			c = json[index++];

			bool complete = false;
			while (!complete) {

				if (index == json.Length) {
					break;
				}

				c = json[index++];
				if (c == '"') {
					complete = true;
					break;
				} else if (c == '\\') {

					if (index == json.Length) {
						break;
					}
					c = json[index++];
					if (c == '"') {
						s +=('"');
					} else if (c == '\\') {
						s +=('\\');
					} else if (c == '/') {
						s +=('/');
					} else if (c == 'b') {
						s +=('\b');
					} else if (c == 'f') {
						s +=('\f');
					} else if (c == 'n') {
						s +=('\n');
					} else if (c == 'r') {
						s +=('\r');
					} else if (c == 't') {
						s +=('\t');
					} else if (c == 'u') {
						int remainingLength = json.Length - index;
						if (remainingLength >= 4) {
							// parse the 32 bit hex into an integer codepoint
							uint codePoint = UInt32.Parse(new string(json, index, 4));
							// convert the integer codepoint to a unicode char and add to string
							s +=(codePoint);
							// skip 4 chars
							index += 4;
						} else {
							break;
						}
					}

				} else {
					s +=(c);
				}

			}

			if (!complete) {
				success = false;
				return null;
			}


            return s.ToString();
		}


        protected static object ParseString(FileStream jsonStream, ref bool success, ref JsonCustomObjectsDefinition jco)
        {
            return jco.StringProperty.NewString(ParseString(jsonStream, ref success));
        }
        protected static string ParseString(FileStream jsonStream, ref bool success)
        {
            String s = "";
            char c;

            EatWhitespace(jsonStream);

            // "
            c = (char)jsonStream.ReadByte();

            bool complete = false;
            while (!complete)
            {

                if (jsonStream.Position == jsonStream.Length)
                {
                    break;
                }

                c = (char)jsonStream.ReadByte();
                if (c == '"')
                {
                    complete = true;
                    break;
                }
                else if (c == '\\')
                {

                    if (jsonStream.Position == jsonStream.Length)
                    {
                        break;
                    }
                    c = (char)jsonStream.ReadByte();
                    if (c == '"')
                    {
                        s += ('"');
                    }
                    else if (c == '\\')
                    {
                        s += ('\\');
                    }
                    else if (c == '/')
                    {
                        s += ('/');
                    }
                    else if (c == 'b')
                    {
                        s += ('\b');
                    }
                    else if (c == 'f')
                    {
                        s += ('\f');
                    }
                    else if (c == 'n')
                    {
                        s += ('\n');
                    }
                    else if (c == 'r')
                    {
                        s += ('\r');
                    }
                    else if (c == 't')
                    {
                        s += ('\t');
                    }
                    else if (c == 'u')
                    {
                        long remainingLength = jsonStream.Length - jsonStream.Position;
                        if (remainingLength >= 4)
                        {
                            // parse the 32 bit hex into an integer codepoint
                            string json = "";
                            for (int i = 0; i < 4; i++)
                            {
                                json += (char)jsonStream.ReadByte();
                            }
                            uint codePoint = UInt32.Parse(json);
                            // convert the integer codepoint to a unicode char and add to string
                            s += (codePoint);
                        }
                        else
                        {
                            break;
                        }
                    }

                }
                else
                {
                    s += (c);
                }

            }

            if (!complete)
            {
                success = false;
                return null;
            }
            success = true;
            return s.ToString();
        }

        protected static object ParseNumber(char[] json, ref int index, ref bool success, ref JsonCustomObjectsDefinition jco)
        {
                var n =jco.FloatProperty.NewFloat(ParseNumber(json, ref index, ref success));
                return n;
        }

        protected static double ParseNumber(char[] json, ref int index, ref bool success)
        {
			EatWhitespace(json, ref index);

			int lastIndex = GetLastIndexOfNumber(json, index);
			int charLength = (lastIndex - index) + 1;

			double number;
			success = Double.TryParse(new string(json, index, charLength), out number);

			index = lastIndex + 1;
			return number;
		}

        protected static object ParseNumber(FileStream jsonStream, ref bool success, ref JsonCustomObjectsDefinition jco)
        {
            var d = ParseNumber(jsonStream, ref success);
            var n = jco.FloatProperty.NewFloat(d);
            return n;
        }

        protected static double ParseNumber(FileStream jsonStream, ref bool success)
        {
            EatWhitespace(jsonStream);

            long lastIndex = GetLastIndexOfNumber(jsonStream);
            int charLength = (int)(lastIndex - jsonStream.Position) + 1;
            string json = "";
            for (int i = 0; i < charLength; i++)
            {
                json += (char)jsonStream.ReadByte();
            }
            double number;
            success = Double.TryParse(json, out number);

            return number;
        }


		protected static int GetLastIndexOfNumber(char[] json, int index)
		{
			int lastIndex;

			for (lastIndex = index; lastIndex < json.Length; lastIndex++) {
				if ("0123456789+-.eE".IndexOf(json[lastIndex]) == -1) {
					break;
				}
			}
			return lastIndex - 1;
		}

        protected static long GetLastIndexOfNumber(FileStream jsonStream)
        {
            long saveIndex = jsonStream.Position;
            long lastIndex = saveIndex;
            while (jsonStream.Position < jsonStream.Length)
            {
                if ("0123456789+-.eE".IndexOf((char)jsonStream.ReadByte()) == -1)
                {
                    lastIndex = jsonStream.Position-2;
                    jsonStream.Seek(saveIndex, SeekOrigin.Begin);
                    break;
                }
            }
            
            return lastIndex;
        }

        protected static void EatWhitespace(char[] json, ref int index)
        {
            for (; index < json.Length; index++)
            {
                if (" \t\n\r".IndexOf(json[index]) == -1)
                {
                    break;
                }
            }
        }

        protected static void EatWhitespace(FileStream  jsonStream)
        {
            while (jsonStream.Position <jsonStream.Length)
            {
                if (" \t\n\r".IndexOf((char)jsonStream.ReadByte()) == -1)
                {
                    jsonStream.Seek(-1,SeekOrigin.Current);
                    break;
                }
            }
        }

        protected static int LookAhead(char[] json, int index)
        {
            int saveIndex = index;
            return NextToken(json, ref saveIndex);
        }

        protected static int LookAhead(FileStream jsonStream)
        {
            long saveIndex = jsonStream.Position;
            var ret = NextToken(jsonStream);
            jsonStream.Seek(saveIndex, SeekOrigin.Begin);
            return ret;
        }

        protected static int NextToken(char[] json, ref int index)
		{
			EatWhitespace(json, ref index);

			if (index == json.Length) {
				return JSON.TOKEN_NONE;
			}
            int ret = GetTokenChar(json[index]);
            if(ret!= JSON.TOKEN_NONE)
            {
                index++;
                return ret;
            }
            return GetTokenWord(json, ref index);
        }


		protected static int NextToken(FileStream jsonStream)
		{
        	EatWhitespace(jsonStream);

			if (jsonStream.Position == jsonStream.Length) {
				return JSON.TOKEN_NONE;
			}
            int ret = GetTokenChar((char)jsonStream.ReadByte());
            if(ret!= JSON.TOKEN_NONE)return ret;
            jsonStream.Seek(-1,SeekOrigin.Current);
            long max = jsonStream.Length - jsonStream.Position;
            if(max >5) max = 5;
            int index;
            char[] json = {' ',' ',' ',' ',' '};
            for (index = 0; index < max; index++)
            {
                json[index]=(char)jsonStream.ReadByte();
            }
            index = 0;
            ret = GetTokenWord(json, ref index);
            jsonStream.Seek(index - max, SeekOrigin.Current);                        
            return ret;
        }

        protected static int GetTokenChar(char c)
		{

			switch (c) {
				case '{':
					return JSON.TOKEN_CURLY_OPEN;
				case '}':
					return JSON.TOKEN_CURLY_CLOSE;
				case '[':
					return JSON.TOKEN_SQUARED_OPEN;
				case ']':
					return JSON.TOKEN_SQUARED_CLOSE;
				case ',':
					return JSON.TOKEN_COMMA;
				case '"':
					return JSON.TOKEN_STRING;
				case '0': case '1': case '2': case '3': case '4':
				case '5': case '6': case '7': case '8': case '9':
				case '-':
					return JSON.TOKEN_NUMBER;
				case ':':
					return JSON.TOKEN_COLON;
			}
            return JSON.TOKEN_NONE;
        }

        protected static int GetTokenWord(char[] json, ref int index)
        {

			int remainingLength = json.Length - index;

			// false
			if (remainingLength >= 5) {
				if (json[index] == 'f' &&
					json[index + 1] == 'a' &&
					json[index + 2] == 'l' &&
					json[index + 3] == 's' &&
					json[index + 4] == 'e') {
					index += 5;
					return JSON.TOKEN_FALSE;
				}
			}

			// true
			if (remainingLength >= 4) {
				if (json[index] == 't' &&
					json[index + 1] == 'r' &&
					json[index + 2] == 'u' &&
					json[index + 3] == 'e') {
					index += 4;
					return JSON.TOKEN_TRUE;
				}
			}

			// null
			if (remainingLength >= 4) {
				if (json[index] == 'n' &&
					json[index + 1] == 'u' &&
					json[index + 2] == 'l' &&
					json[index + 3] == 'l') {
					index += 4;
					return JSON.TOKEN_NULL;
				}
			}

			return JSON.TOKEN_NONE;
		}



		protected static bool SerializeValue(object value, StringBuilder builder)
		{
			bool success = true;

			if (value is string) {
				success = SerializeString((string)value, builder);
			} else if (value is Hashtable) {
				success = SerializeObject((Hashtable)value, builder);
			} else if (value is ArrayList) {
				success = SerializeArray((ArrayList)value, builder);
			} else if ((value is Boolean) && ((Boolean)value == true)) {
				builder.Append("true");
			} else if ((value is Boolean) && ((Boolean)value == false)) {
				builder.Append("false");
			} else if (value is ValueType) {
				// thanks to ritchie for pointing out ValueType to me
				success = SerializeNumber(Convert.ToDouble(value.ToString()), builder);
			} else if (value == null) {
				builder.Append("null");
			} else {
				success = false;
			}
			return success;
		}

		protected static bool SerializeObject(Hashtable anObject, StringBuilder builder)
		{
			builder.Append("{");

			//IEnumerator e = anObject.GetEnumerator();
			bool first = true;
			foreach (DictionaryEntry e in anObject)
			{
				string key = e.Key.ToString();
				object value = e.Value;

				if (!first)
					builder.Append(", ");

				SerializeString(key, builder);
				builder.Append(":");
				if (!SerializeValue(value, builder))
					return false;

				first = false;
			}
			builder.Append("}");
			return true;
		}

		protected static bool SerializeArray(ArrayList anArray, StringBuilder builder)
		{
			builder.Append("[");

			bool first = true;
			for (int i = 0; i < anArray.Count; i++) {
				object value = anArray[i];

				if (!first) {
					builder.Append(", ");
				}

				if (!SerializeValue(value, builder)) {
					return false;
				}

				first = false;
			}

			builder.Append("]");
			return true;
		}

		protected static bool SerializeString(string aString, StringBuilder builder)
		{
			builder.Append("\"");

			char[] charArray = aString.ToCharArray();
			for (int i = 0; i < charArray.Length; i++) {
				char c = charArray[i];
				if (c == '"') {
					builder.Append("\\\"");
				} else if (c == '\\') {
					builder.Append("\\\\");
				} else if (c == '\b') {
					builder.Append("\\b");
				} else if (c == '\f') {
					builder.Append("\\f");
				} else if (c == '\n') {
					builder.Append("\\n");
				} else if (c == '\r') {
					builder.Append("\\r");
				} else if (c == '\t') {
					builder.Append("\\t");
				} else {
					int codepoint = Convert.ToInt32(((byte)c).ToString());
					if ((codepoint >= 32) && (codepoint <= 126)) {
						builder.Append(c);
					} else {
                        //builder.Append("\\u" + codepoint.ToString("{0:X}").PadLeft(4, '0'));
                        builder.Append("\\u" + "0000" + codepoint.ToString("{0:X}"));
                    }
				}
			}

			builder.Append("\"");
			return true;
		}

		protected static bool SerializeNumber(double number, StringBuilder builder)
		{
			
			builder.Append(number.ToString());
			return true;
		}

        public static void ArrayListAdd(Object array, Object value)
        {
            (array as ArrayList).Add(value);
        }

        public static void HashTableSet(Object table, string name, Object value)
        {
            (table as Hashtable)[name] = value;
        }
    }

    
    public class JsonCustomObjectsDefinition
    {
        private IJsonArrayList _arrayList = new JsonArrayList();
        private IJsonHashTable _hashTable = new JsonHashTable();
        private IJsonStringProperty _stringProperty = new JsonStringProperty();
        private IJsonIntProperty _intProperty = new JsonIntProperty();
        private IJsonFloatProperty _floatProperty = new JsonFloatProperty();
        private IJsonBoolProperty _boolProperty = new JsonBoolProperty();
        private IJsonDatetimeProperty _datetimeProperty= new JsonDatetimeProperty();

        public JsonCustomObjectsDefinition(){}

        public JsonCustomObjectsDefinition(IJsonArrayList arrayList,
                                IJsonHashTable hashTable,
                                IJsonStringProperty stringProperty,
                                IJsonIntProperty intProperty,
                                IJsonFloatProperty floatProperty,
                                IJsonBoolProperty boolProperty,
                                IJsonDatetimeProperty datetimeProperty)
        {
            if(arrayList!=null)_arrayList=arrayList;
            if(hashTable!=null)_hashTable= hashTable;
            if(stringProperty!=null)_stringProperty=stringProperty;
            if(intProperty!=null)_intProperty=intProperty;
            if(floatProperty!=null)_floatProperty=floatProperty;
            if(boolProperty!=null)_boolProperty=boolProperty;
            if (datetimeProperty != null) _datetimeProperty = datetimeProperty;
        }

        public IJsonArrayList ArrayList { get { return _arrayList; } }
        public IJsonHashTable HashTable { get { return _hashTable; } }
        public IJsonStringProperty StringProperty { get { return _stringProperty; } }
        public IJsonIntProperty IntProperty { get { return _intProperty; } }
        public IJsonFloatProperty FloatProperty { get { return _floatProperty; } }
        public IJsonBoolProperty BoolProperty { get { return _boolProperty; } }
        public IJsonDatetimeProperty DatetimeProperty { get { return _datetimeProperty; } }
    }


    public interface IJsonArrayList
    {
        object NewArrayList();
        int Add(object arrayList, object obj);
        void Clear(object arrayList);
        bool Contains(object arrayList, object obj);
        int Count(object arrayList);
        void Remove(object arrayList, object obj);
        void RemoveAt(object arrayList, int i);
    }

    public class JsonArrayList : IJsonArrayList
    {
        public object NewArrayList(){ return new ArrayList(); }
        public int Add(object arrayList, object obj) { return ((ArrayList)arrayList).Add(obj); }
        public void Clear(object arrayList) { ((ArrayList)arrayList).Clear(); }
        public bool Contains(object arrayList, object obj) { return ((ArrayList)arrayList).Contains(obj); }
        public int Count(object arrayList) { return ((ArrayList)arrayList).Count; }
        public void Remove(object arrayList, object obj) { ((ArrayList)arrayList).Remove(obj); }
        public void RemoveAt(object arrayList, int i) { ((ArrayList)arrayList).RemoveAt(i); }
    }

    public interface IJsonHashTable
    {
        object NewHashTable();
        void Clear(object hashTable);
        bool Contains(object hashTable, object key);
        int Count(object hashTable);
        void Remove(object hashTable, object key);
        void Set(object hashTable, object key, object value);
    }

    public class JsonHashTable : IJsonHashTable
    {
        public object NewHashTable() { return new Hashtable(); }
        public void Clear(object hashTable) { ((Hashtable)hashTable).Clear(); }
        public bool Contains(object hashTable, object key) { return ((Hashtable)hashTable).Contains(key); }
        public int Count(object hashTable) { return ((Hashtable)hashTable).Count; }
        public void Remove(object hashTable, object key) { ((Hashtable)hashTable).Remove(key); }
        public void Set(object hashTable, object key, object value) { ((Hashtable)hashTable)[key] = value; }
    }

    public interface IJsonStringProperty
    {
        object NewString();
        object NewString(string s);
        string GetValue(object obj);
        void SetValue(ref object obj, string s);
    }

    public class JsonStringProperty : IJsonStringProperty
    {
        public object NewString()
        {
            return (string)"";
        }
        public object NewString(string s)
        {
            string ret=s;
            return ret;
        }
        public string GetValue(object obj)
        {
            return (string)obj;
        }
        public void SetValue(ref object obj, string s)
        {
            obj = s;
        }
    }

    public interface IJsonIntProperty
    {
        object NewInt();
    }

    public class JsonIntProperty : IJsonIntProperty
    {
        public object NewInt()
        {
            return (int)0;
        }
    }

    public interface IJsonFloatProperty
    {
        object NewFloat();
        object NewFloat(double d);
        double GetValue(object obj);
        void SetValue(ref object obj, double d);
    }

    public class JsonFloatProperty : IJsonFloatProperty
    {
        public object NewFloat()
        {
            return (double)0.0;
        }
        public object NewFloat(double d)
        {
            double ret = d;
            return ret;
        }
        public double GetValue(object obj)
        {
            return (double)obj;
        }
        public void SetValue(ref object obj, double d)
        {
            obj = d;
        }
    }

    public interface IJsonBoolProperty
    {
        object NewBool();
        object NewBool(bool b);
        bool GetValue(object obj);
        void SetValue(ref object obj, bool b);
    }

    public class JsonBoolProperty : IJsonBoolProperty
    {
        public object NewBool()
        {
            return (bool)false;
        }
        public object NewBool(bool b)
        {
            bool ret = b;
            return ret;
        }
        public bool GetValue(object obj)
        {
            return (bool)obj;
        }
        public void SetValue(ref object obj, bool b)
        {
            obj = b;
        }
    }

    public interface IJsonDatetimeProperty
    {
        object NewDatetime();
    }

    public class JsonDatetimeProperty : IJsonDatetimeProperty
    {
        public object NewDatetime()
        {
            return new DateTime();
        }
    }
}

