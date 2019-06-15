/*
 * Secalot RemoteScreen.
 * Copyright (c) 2018 Matvey Mukha <matvey.mukha@gmail.com>
 *
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/.
 */

using Newtonsoft.Json.Linq;
using Org.BouncyCastle.Math;
using Ripple.Core.Types;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RemoteScreen
{
    public class XrpParserException : Exception
    {
        public XrpParserException()
        {
        }

        public XrpParserException(string message)
            : base(message)
        {
        }

        public XrpParserException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }

    class XrpParser
    {

        private static string ConvertXrpAmount(string amountSrting)
        {
            string value = "";
            var valueInXrp = new BigInteger(amountSrting).DivideAndRemainder(new BigInteger("1000000"));
            var valueInXrpRemainder = valueInXrp[1].ToString(10).PadLeft(6, '0').TrimEnd('0');

            if (valueInXrpRemainder == "")
            {
                value = valueInXrp[0].ToString(10) + " XRP";
            }
            else
            {
                value = valueInXrp[0].ToString(10) + "." + valueInXrpRemainder + " XRP";
            }

            return value;
        }

        private static string ParseElement(JContainer element, int numberOfTabs)
        {
            string retVal = "";

            if (element is JObject)
            {
                foreach (KeyValuePair<string, JToken> item in (JObject)element)
                {
                    for (int i = 0; i < numberOfTabs; i++)
                    {
                        retVal += "\t";
                    }

                    retVal += "<b>" + item.Key.First().ToString().ToUpper() + item.Key.Substring(1) + "</b>:";

                    if (item.Value is JValue)
                    {
                        if (item.Key == "Amount")
                        {
                            retVal += " " + ConvertXrpAmount(item.Value.ToString()) + "<br>";
                        }
                        else if (item.Key == "Fee")
                        {
                            retVal += " " + item.Value.ToString() + " drops<br>";
                        }
                        else if ((item.Key == "MemoType") || (item.Key == "MemoFormat"))
                        {
                            byte[] byteArray = item.Value.ToString().ConvertHexStringToByteArray();
                            string asciiString = System.Text.Encoding.ASCII.GetString(byteArray);

                            retVal += " " + asciiString + "<br>";
                        }
                        else if (item.Key == "MemoData")
                        {
                            bool plainFormat = false;

                            if (((JObject)element).ContainsKey("MemoFormat"))
                            {
                                JToken memoFormat = ((JObject)element).GetValue("MemoFormat");
                                if (memoFormat is JValue)
                                {
                                    byte[] byteArray = memoFormat.ToString().ConvertHexStringToByteArray();
                                    string asciiString = System.Text.Encoding.ASCII.GetString(byteArray);

                                    if (asciiString == "plain/text")
                                    {
                                        plainFormat = true;
                                    }
                                }
                            }

                            if (plainFormat == true)
                            {
                                byte[] byteArray = item.Value.ToString().ConvertHexStringToByteArray();
                                string asciiString = System.Text.Encoding.ASCII.GetString(byteArray);

                                retVal += " " + asciiString + "<br>";
                            }
                            else
                            {
                                retVal += " " + item.Value.ToString() + "<br>";
                            }
                        }
                        else
                        {
                            retVal += " " + item.Value.ToString() + "<br>";
                        }
                    }
                    else
                    {
                        retVal += "<br>" + ParseElement((JContainer)item.Value, numberOfTabs + 1);
                    }
                }
            }
            else if (element is JArray)
            {
                foreach (JToken el in (JArray)element)
                {
                    if (el is JValue)
                    {
                        retVal += " " + el.ToString() + "<br>";
                    }
                    else
                    {
                        retVal += ParseElement((JContainer)el, numberOfTabs);
                    }
                }
            }

            return retVal;
        }

        public static List<string> ParseRippleTransaction(RippleTransactionInfo info, byte[] transaction, ref uint timeout)
        {
            string htmlOutput = "";
            string htmlOutputWithDetails = "";

            if (info.transactionTooBigToDisplay == true)
            {
                throw new XrpParserException("Transaction too big to display.");
            }

            string transactionString = BitConverter.ToString(transaction).Replace("-", "").ToLower();

            var tx = StObject.FromHex(transactionString);
            JToken fullTxJson = tx.ToJson();

            if (!(fullTxJson is JObject))
            {
                throw new XrpParserException("Invalid transaction.");
            }

            JToken mainFieldsJson = fullTxJson.DeepClone();

            foreach (JProperty element in fullTxJson.Children())
            {
                string name = (element).Name;

                if ((name.ToLower() == "maxledgerversion") || (name.ToLower() == "maxledgerversionoffset") ||
                    (name.ToLower() == "flags") || (name.ToLower() == "sequence") || (name.ToLower() == "signingpubkey") ||
                    (name.ToLower() == "account"))
                {
                    ((JObject)mainFieldsJson).Property(name).Remove();
                }
            }

            htmlOutput += ParseElement((JContainer)mainFieldsJson, 0);
            htmlOutputWithDetails += ParseElement((JContainer)fullTxJson, 0);

            List<string> retVal = new List<string>();

            retVal.Add(htmlOutput);
            retVal.Add(htmlOutputWithDetails);

            timeout = (info.remainingTime / 1000);

            return retVal;
        }
    }
}
