﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using WWarehouseManagement.Database;

namespace WarehouseManagement.Controller
{
    public class Trial_Controller
    {
        static sql_control sql = new sql_control();
        public static void InsertTrialDay() {
            sql.Query("EXEC Sp_Trial_Insertion");
        }
        public static void checkOffice()
        {
            sql.Query($"SELECT * FROM tbl_trial_key");
            if (sql.HasException(true)) return;
            if(sql.DBDT.Rows.Count > 0)
            {
                foreach(DataRow dr in sql.DBDT.Rows)
                {
                    if (dr[1].ToString() == "OFFICE")
                    {
                        sql.Query($"DELETE FROM tbl_trial");
                    }
                }
            }
        }
        public static void setLifetime()
        {
            sql.Query($"SELECT customer_id FROM tbl_couriers WHERE customer_id = 'MNL-V0619'");
            if (sql.DBDT.Rows.Count > 0)
            {
                sql.Query($"UPDATE tbl_trial_key set Product_Key = 'OFFICE'");
            }
            else
            {
                //do nothing
            }
        }
        public static int HaveTrialKey() => int.Parse(sql.ReturnResult("EXEC SpTrial_HaveKey"));
        public static void MessagePopup()
        {
            //if (Trial_Controller.IsTrialEnded())
            //    MessageBox.Show("Trial is expired. Please contact your distributor of the application.");
        }
        public static bool IsTrialEnded()
        {
            int days = int.Parse(sql.ReturnResult("EXEC Sp_Trial_Validation"));
            if (IsSubscribed())
            {
                if (days >= 30)
                    return true;
                else
                    return false;
            }
            else
            {
                if (days >= 7)
                    return true;
                else
                    return false;
            }
        }
        public static void checkTrialKey()
        {
            int count = int.Parse(sql.ReturnResult($"SELECT COUNT(*) from tbl_trial_key WHERE Product_Key != 'YES' AND Product_Key != 'NO' AND Product_Key != 'Office'"));
            if (count > 0)
            {
                sql.Query($"DELETE FROM tbl_trial_key WHERE Product_Key != 'YES' AND Product_Key != 'NO' AND Product_Key != 'Office'");
                sql.Query($"INSERT INTO tbl_trial_key (Product_Key) VALUES ('No')");
            }
            int checkKey = int.Parse(sql.ReturnResult($"SELECT COUNT(*) FROM tbl_trial_key"));
            if (checkKey == 0)
            {
                sql.Query($"INSERT INTO tbl_trial_key (Product_Key) VALUES ('No')");
            }
        }
        public static void checkTrialCount()
        {

            int count = int.Parse(sql.ReturnResult($"SELECT COUNT(*) FROM tbl_trial"));
            if (count == 0)
            {
                sql.Query($"INSERT INTO tbl_trial (date) VALUES (GETDATE())");
            }
            else
            {
                //do nothing
            }
        }
        public static void refreshSubs()
        {
            sql.Query($"DELETE FROM tbl_trial");
            sql.Query($"UPDATE tbl_trial_key SET Product_Key = 'Yes' WHERE Product_Key = 'No'");
            sql.Query($"INSERT INTO tbl_trial (date) VALUES (GETDATE())");
        }
        public static bool IsSubscribed()
        {
            sql.Query(@"
                DECLARE @DataCount INT = (SELECT COUNT(*) FROM tbl_trial_key)
                IF @DataCount = 0
                BEGIN
	                INSERT INTO tbl_trial_key(Product_Key) VALUES ('No')
                END;
            ");

            string status = sql.ReturnResult("SELECT TOP 1 Product_Key FROM tbl_trial_key ");
            if (status == "No")
                return false;
            else
                return true;
        }
        public static void updateModules()
        {
            int? count = int.Parse(sql.ReturnResult($"SELECT COUNT(*) from tbl_module_access WHERE module_name = 'modify order inquiry'"));
            if (count > 0)
            {
                sql.Query($"UPDATE tbl_module_access SET module_name = 'Modify Out For Pick Up' WHERE module_name = 'modify order inquiry'");
                sql.Query($"UPDATE tbl_module_access SET module_name = 'View Out For Pick Up' WHERE module_name = 'view order inquiry'");
            }
            int? count1 = int.Parse(sql.ReturnResult($"SELECT COUNT(*) FROM tbl_trial_key"));
            if (count1 > 1)
            {
                sql.Query($"DELETE FROM tbl_trial_key");
                sql.Query($"INSERT INTO tbl_trial_key(Product_Key) VALUES ('No')");
            }
        }
    }
}
