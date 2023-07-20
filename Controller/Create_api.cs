﻿using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using WarehouseManagement.Models;
using WarehouseManagement.Views.Main.OrderModule.CustomDialogs.NewOrder;
using System.Data;
using Newtonsoft.Json.Linq;
using WarehouseManagement.Database;
using WWarehouseManagement.Database;
using System.IO;
using System.Security.Cryptography;

namespace WarehouseManagement.Controller
{
    public class Create_api
    {
        sql_control sql = new sql_control();
        db_queries queries = new db_queries();
        SuspiciousController suspiciouscontroller = new SuspiciousController();
        public async Task<bool> api_create(Receiver receiver, Booking_info booking_Info, bool suspicious)
        {
            string url = "https://jtapi.jtexpress.ph/jts-phl-order-api/api/order/create";
            string key = Decrypt(GlobalModel.key);
            string logistics_interface = @"
            {
            ""actiontype"": ""add"",
            ""environment"": ""production:yes"",
            ""eccompanyid"": ""THIRDYNAL"",
            ""customerid"": ""CS-V0234"",
            ""txlogisticid"": ""1547191-2707123"",
            ""ordertype"": ""1"",
            ""servicetype"": ""6"",
            ""deliverytype"": ""1"",
            ""sender"": {
                ""name"": ""grace"",
                ""mobile"": ""09951455616"",
                ""prov"": ""METRO-MANILA"",
                ""city"": ""TAGUIG"",
                ""area"": ""BAGUMBAYAN"",
                ""address"": ""#20 1st AVE. STA. MARIA INDUSTRIAL BAGUMBAYAN TAGUIG CITY""
            },
            ""receiver"": {
                ""name"": ""Kathea de guzman"",
                ""mobile"": ""0917 536 7871"",
                ""phone"": """",
                ""prov"": ""METRO-MANILA"",
                ""city"": ""QUEZON-CITY"",
                ""area"": ""BAGONG LIPUNAN"",
                ""address"": ""#20 1st AVE. STA. MARIA INDUSTRIAL BAGUMBAYAN TAGUIG CITY""
            },
            ""createordertime"": ""2020-07-09 22:48:26"",
            ""paytype"": ""1"",
            ""weight"": ""0.1"",
            ""itemsvalue"": ""1599"",
            ""totalquantity"": ""1"",
            ""remark"": """",
            ""items"": [
                {
                    ""itemname"": ""Strap wide leg pants"",
                    ""number"": ""1"",
                    ""itemvalue"": ""1599.000"",
                    ""desc"": ""Strap wide leg pants""
                }
            ]
            }";
            string msg_type = "ORDERCREATE";

            dynamic payloadObj = Newtonsoft.Json.JsonConvert.DeserializeObject(logistics_interface);

            //for VIP code
            payloadObj.eccompanyid = GlobalModel.eccompany_id;
            payloadObj.customerid = GlobalModel.customer_id;

            //updating sender information
            payloadObj.sender.name = GlobalModel.sender_name;
            payloadObj.sender.phone = GlobalModel.sender_phone;
            payloadObj.sender.mobile = GlobalModel.sender_phone;
            payloadObj.sender.prov = GlobalModel.sender_province;
            payloadObj.sender.city = GlobalModel.sender_city;
            payloadObj.sender.area = GlobalModel.sender_area;
            payloadObj.sender.address = GlobalModel.sender_address;

            //updating receiver information
            payloadObj.receiver.name = receiver.FirstName + " " + receiver.MiddleName + " " + receiver.LastName;
            payloadObj.receiver.phone = receiver.Phone;
            payloadObj.receiver.mobile = receiver.Phone;
            payloadObj.receiver.prov = receiver.Province;
            payloadObj.receiver.city = receiver.City;
            payloadObj.receiver.area = receiver.Barangay;
            payloadObj.receiver.address = receiver.Address;
            payloadObj.txlogisticid = "TECS-" + GenerateTransactionID();

            //updating other fields
            payloadObj.createordertime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            payloadObj.weight = booking_Info.weight;
            payloadObj.itemsvalue = booking_Info.goods_value;
            payloadObj.totalquantity = booking_Info.quantity;
            payloadObj.remark = booking_Info.remarks;

            //updating items field
            var itemsArray = payloadObj["items"] as JArray; // Assuming payloadObj is your JSON object
            if (itemsArray != null && itemsArray.Count > 0)
            {
                var firstItem = itemsArray[0];
                firstItem["itemname"] = booking_Info.item_name;
                firstItem["number"] = booking_Info.quantity;
                firstItem["itemvalue"] = booking_Info.goods_value;
            }


            string updatedPayload = Newtonsoft.Json.JsonConvert.SerializeObject(payloadObj);
            //MessageBox.Show(updatedPayload);
            try
            {
                // Step 3: Sign the JSON content and secret key
                string data_digest = MD5Util.GetMD5Hash(updatedPayload + key);
                string encodedDataDigest = Convert.ToBase64String(Encoding.UTF8.GetBytes(data_digest));

                // Step 4: Send HTTP POST request
                using (WebClient client = new WebClient())
                {
                    // Prepare request parameters
                    NameValueCollection requestData = new NameValueCollection();
                    requestData.Add("logistics_interface", updatedPayload);
                    requestData.Add("data_digest", encodedDataDigest);
                    requestData.Add("msg_type", msg_type);
                    requestData.Add("eccompanyid", GlobalModel.eccompany_id);

                    // Send the POST request
                    byte[] responseBytes = await client.UploadValuesTaskAsync(url, requestData);

                    // Decode and display the response
                    string response = Encoding.UTF8.GetString(responseBytes);

                    //to decode the response
                    dynamic responseObject = Newtonsoft.Json.JsonConvert.DeserializeObject(response);
                    string logisticProviderId = responseObject.logisticproviderid;
                    dynamic responseItems = responseObject.responseitems[0];
                    string success = responseItems.success;
                    string reason = responseItems.reason;
                    string txLogisticId = responseItems.txlogisticid;
                    string mailNo = responseItems.mailno;
                    string sortingCode = responseItems.sortingcode;

                    // Store the parameters in separate strings
                    string logisticProviderIdString = logisticProviderId.ToString();
                    string successString = success.ToString();
                    string reasonString = reason.ToString();
                    
                    //if there is no error
                    if (successString == "true")
                    {
                        string txLogisticIdString = txLogisticId.ToString();
                        string mailNoString = mailNo.ToString();
                        string sortingCodeString = sortingCode.ToString();

                        //true = insert suspicious
                        if (suspicious)
                        {
                            queries.insert_receiver(receiver);

                            queries.Insert_Orders(txLogisticIdString, mailNoString, booking_Info, "PENDING");

                            queries.insert_Incentives(booking_Info, txLogisticIdString);

                            queries.update_inventory_status(booking_Info);

                            suspiciouscontroller.InsertSuspiciousData();

                        }
                        else
                        {

                            queries.insert_receiver(receiver);

                            queries.Insert_Orders(txLogisticIdString, mailNoString, booking_Info, "PENDING");

                            queries.insert_Incentives(booking_Info, txLogisticIdString);

                            queries.update_inventory_status(booking_Info);
                        }

                        MessageBox.Show("Order has been Created");
                        return true;
                    }
                    //if there's error on API
                    else
                    {
                        MessageBox.Show(reason + " Please double check the details provided.");
                        return false;
                    }

                }
                
            }
            catch (Exception ex)
            {
                MessageBox.Show("An error occurred: " + ex.Message);
                return false;
            }
        }
        //api bulk orders
        public async Task create_bulk_api(List<bulk_model> model, Button btn, bool granted)
        {
            btn.IsEnabled = false;
            await Task.Run(async () =>
            {
                string url = "https://jtapi.jtexpress.ph/jts-phl-order-api/api/order/create";
                string key = Decrypt(GlobalModel.key);
                string logistics_interface = @"
                    {
                        ""actiontype"": ""add"",
                        ""environment"": ""production:yes"",
                        ""eccompanyid"": ""THIRDYNAL"",
                        ""customerid"": ""CS-V0234"",
                        ""txlogisticid"": ""1547191-2707123"",
                        ""ordertype"": ""1"",
                        ""servicetype"": ""6"",
                        ""deliverytype"": ""1"",
                        ""sender"": {
                            ""name"": ""grace"",
                            ""mobile"": ""09951455616"",
                            ""prov"": ""METRO-MANILA"",
                            ""city"": ""TAGUIG"",
                            ""area"": ""BAGUMBAYAN"",
                            ""address"": ""#20 1st AVE. STA. MARIA INDUSTRIAL BAGUMBAYAN TAGUIG CITY""
                        },
                        ""receiver"": {
                            ""name"": ""Kathea de guzman"",
                            ""mobile"": ""0917 536 7871"",
                            ""phone"": """",
                            ""prov"": ""METRO-MANILA"",
                            ""city"": ""QUEZON-CITY"",
                            ""area"": ""BAGONG LIPUNAN"",
                            ""address"": ""#20 1st AVE. STA. MARIA INDUSTRIAL BAGUMBAYAN TAGUIG CITY""
                        },
                        ""createordertime"": ""2020-07-09 22:48:26"",
                        ""paytype"": ""1"",
                        ""weight"": ""0.1"",
                        ""itemsvalue"": ""1599"",
                        ""totalquantity"": ""1"",
                        ""remark"": """",
                        ""items"": [
                            {
                                ""itemname"": ""Strap wide leg pants"",
                                ""number"": ""1"",
                                ""itemvalue"": ""1599.000"",
                                ""desc"": ""Strap wide leg pants""
                            }
                        ]
                    }";
                string msg_type = "ORDERCREATE";

                dynamic payloadObj = Newtonsoft.Json.JsonConvert.DeserializeObject(logistics_interface);


                //for VIP code
                payloadObj.eccompanyid = GlobalModel.eccompany_id;
                payloadObj.customerid = GlobalModel.customer_id;

                //updating sender information
                payloadObj.sender.name = GlobalModel.sender_name;
                payloadObj.sender.phone = GlobalModel.sender_phone;
                payloadObj.sender.mobile = GlobalModel.sender_phone;
                payloadObj.sender.prov = GlobalModel.sender_province;
                payloadObj.sender.city = GlobalModel.sender_city;
                payloadObj.sender.area = GlobalModel.sender_area;
                payloadObj.sender.address = GlobalModel.sender_address;

                //updating receiver information
                foreach(bulk_model details in model)
                {
                    payloadObj.receiver.name = details.receiver_name;
                    payloadObj.receiver.phone = details.receiver_phone;
                    payloadObj.receiver.mobile = details.receiver_phone;
                    payloadObj.receiver.prov = details.receiver_province;
                    payloadObj.receiver.city = details.receiver_city;
                    payloadObj.receiver.area = details.receiver_area;
                    payloadObj.receiver.address = details.receiver_address;
                    payloadObj.txlogisticid = "TECS-" + GenerateTransactionID();

                    //updating other fields
                    payloadObj.createordertime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                    payloadObj.weight = details.weight;
                    payloadObj.itemsvalue = details.total;
                    payloadObj.totalquantity = details.quantity;
                    payloadObj.remark = details.remarks;

                    //updating items field
                    var itemsArray = payloadObj["items"] as JArray; // Assuming payloadObj is your JSON object
                    if (itemsArray != null && itemsArray.Count > 0)
                    {
                        var firstItem = itemsArray[0];
                        firstItem["itemname"] = details.product_name;
                        firstItem["number"] = details.quantity;
                        firstItem["itemvalue"] = details.total;
                    }

                    string updatedPayload = Newtonsoft.Json.JsonConvert.SerializeObject(payloadObj);
                    //MessageBox.Show(updatedPayload);
                    try
                    {
                        // Step 3: Sign the JSON content and secret key
                        string data_digest = MD5Util.GetMD5Hash(updatedPayload + key);
                        string encodedDataDigest = Convert.ToBase64String(Encoding.UTF8.GetBytes(data_digest));

                        // Step 4: Send HTTP POST request
                        using (WebClient client = new WebClient())
                        {
                            // Prepare request parameters
                            NameValueCollection requestData = new NameValueCollection();
                            requestData.Add("logistics_interface", updatedPayload);
                            requestData.Add("data_digest", encodedDataDigest);
                            requestData.Add("msg_type", msg_type);
                            requestData.Add("eccompanyid", GlobalModel.eccompany_id);

                            // Send the POST request
                            byte[] responseBytes = await client.UploadValuesTaskAsync(url, requestData);

                            // Decode and display the response
                            string response = Encoding.UTF8.GetString(responseBytes);

                            //to decode the response
                            dynamic responseObject = Newtonsoft.Json.JsonConvert.DeserializeObject(response);
                            string logisticProviderId = responseObject.logisticproviderid;
                            dynamic responseItems = responseObject.responseitems[0];
                            string success = responseItems.success;
                            string reason = responseItems.reason;
                            string txLogisticId = responseItems.txlogisticid;
                            string mailNo = responseItems.mailno;
                            string sortingCode = responseItems.sortingcode;

                            // Store the parameters in separate strings
                            string logisticProviderIdString = logisticProviderId.ToString();
                            string successString = success.ToString();
                            string reasonString = reason.ToString();

                            //if there is no error
                            if (successString == "true")
                            {
                                string txLogisticIdString = txLogisticId.ToString();
                                string mailNoString = mailNo.ToString();
                                string sortingCodeString = sortingCode.ToString();

                                //true = insert suspicious
                                if (bulk_inserts.bulk_suspicious(details))
                                {

                                    bulk_inserts.bulk_temp_insert(details);
                                    // if VA or USER accept the risks of pushing suspicious orders
                                    if(granted)
                                    {
                                        bulk_inserts.bulk_receiver(details);

                                        bulk_inserts.bulk_orders(details, mailNoString, txLogisticIdString);

                                        bulk_inserts.bulk_incentives(details, txLogisticIdString);

                                        bulk_inserts.bulk_update_quantity(details);

                                        bulk_inserts.bulk_update_stocks(details);

                                        bulk_inserts.insertSuspiciousTable(mailNoString);
                                    }
                                }
                                else
                                {
                                    bulk_inserts.bulk_receiver(details);

                                    bulk_inserts.bulk_orders(details, mailNoString, txLogisticIdString);

                                    bulk_inserts.bulk_incentives(details, txLogisticIdString);

                                    bulk_inserts.bulk_update_quantity(details);

                                    bulk_inserts.bulk_update_stocks(details);
                                }
                            }
                            //if there's error on API
                            else
                            {
                                MessageBox.Show("Please double check the details provided.");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("An error occurred: " + ex.Message);
                    }
                }

            });
            btn.IsEnabled = true;
        }
        public long GenerateTransactionID()
        {
            var finalString = "";
            var chars = "1234567";
            var stringChars = new char[9];
            var random = new Random();
            for (int i = 0; i < stringChars.Length; i++)
            {
                stringChars[i] = chars[random.Next(chars.Length)];
            }
            finalString = new String(stringChars);

            sql.Query($"SELECT * FROM tbl_orders WHERE order_id = '{"TECS-" + finalString}'");
            if (sql.DBDT.Rows.Count == 0)
            {
                return long.Parse(finalString);
            }
            else
            {
                return GenerateTransactionID();
            }
        }
        public static class MD5Util
        {
            public static string GetMD5Hash(string input)
            {
                using (System.Security.Cryptography.MD5 md5 = System.Security.Cryptography.MD5.Create())
                {
                    byte[] inputBytes = Encoding.UTF8.GetBytes(input);
                    byte[] hashBytes = md5.ComputeHash(inputBytes);

                    StringBuilder builder = new StringBuilder();
                    for (int i = 0; i < hashBytes.Length; i++)
                    {
                        builder.Append(hashBytes[i].ToString("x2"));
                    }

                    return builder.ToString();
                }
            }
        }
        public static string Decrypt(string encryptedText)
        {
            string key = "YourEncryptionKey"; // Replace with your desired encryption key

            byte[] encryptedBytes = Convert.FromBase64String(encryptedText);
            byte[] keyBytes = Encoding.UTF8.GetBytes(key);

            for (int i = 0; i < encryptedBytes.Length; i++)
            {
                encryptedBytes[i] = (byte)(encryptedBytes[i] ^ keyBytes[i % keyBytes.Length]);
            }

            return Encoding.UTF8.GetString(encryptedBytes);
        }
    }
}
