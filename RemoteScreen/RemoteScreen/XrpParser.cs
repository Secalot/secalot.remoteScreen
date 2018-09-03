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

        public static List<string> ParseRippleTransaction(RippleTransactionInfo info, byte[] transaction)
        {
            string htmlOutput = "";
            string htmlOutputWithDetails = "";

            if (info.transactionTooBigToDisplay == true)
            {
                throw new XrpParserException("Transaction too big to display.");
            }

            string transactionString = BitConverter.ToString(transaction).Replace("-", "").ToLower();

            var tx = StObject.FromHex(transactionString);
            JToken txJson = tx.ToJson();

            if (!(txJson is JObject))
            {
                throw new XrpParserException("Invalid transaction.");
            }

            var type = txJson["TransactionType"];

            if ((type != null) && (type.ToString() == "Payment"))
            {
                JObject mainFields = new JObject();

                foreach (JProperty element in txJson.Children())
                {
                    string name = (element).Name;

                    if ((name == "Fee") || (name == "Amount") || (name == "Destination") || (name == "TransactionType"))
                    {
                        mainFields.Add(element);
                    }
                }

                ((JObject)txJson).Property("Fee").Remove();
                ((JObject)txJson).Property("Amount").Remove();
                ((JObject)txJson).Property("Destination").Remove();
                ((JObject)txJson).Property("TransactionType").Remove();

                JObject sortedMainFields = new JObject();
                sortedMainFields.Add(((JObject)mainFields).Property("TransactionType"));
                sortedMainFields.Add(((JObject)mainFields).Property("Destination"));
                sortedMainFields.Add(((JObject)mainFields).Property("Amount"));
                sortedMainFields.Add(((JObject)mainFields).Property("Fee"));

                htmlOutput += ParseElement(sortedMainFields, 0);

                htmlOutputWithDetails += htmlOutput;
                htmlOutputWithDetails += "<br>" + ParseElement((JContainer)txJson, 0);

                htmlOutputWithDetails += "<br><br>";
                htmlOutputWithDetails += "Time remaining to confirm: <b>" + (info.remainingTime / 1000).ToString() + "</b> seconds. <br><br>";
            }
            else
            {
                htmlOutput += ParseElement((JContainer)txJson, 0);
            }

            htmlOutput += "<br><br>";
            htmlOutput += "Time remaining to confirm: <b>" + (info.remainingTime / 1000).ToString() + "</b> seconds. <br><br>";

            List<string> retVal = new List<string>();

            retVal.Add(htmlOutput);

            if (htmlOutputWithDetails != "")
            {
                retVal.Add(htmlOutputWithDetails);
            }

            return retVal;
        }
    }
}
