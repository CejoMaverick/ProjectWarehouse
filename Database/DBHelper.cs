﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Transactions;
using System.Windows;
using WarehouseManagement.Models;

namespace WarehouseManagement.Database
{
    internal class DBHelper : DatabaseConnection
    {

        public async Task<DataTable?> GetTableData(string tableName, IEnumerable<string> columns)
        {
            using DatabaseConnection dbConnection = new();

            string columnsStr = string.Join(", ", columns);
            string query = $"SELECT {columnsStr} FROM {tableName}";

            SqlConnection? connection = await dbConnection.OpenConnection();

            try
            {
                using SqlCommand command = new(query, connection);
                command.Parameters.AddWithValue("@TableName", tableName);

                using SqlDataReader reader = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess);
                DataTable dataTable = new();
                dataTable.Load(reader);
                return dataTable;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return null;
            }
            finally
            {
                CloseConnection();
            }
        }

        public async Task<DataTable> GetTableFiltered(string tableName, IEnumerable<string> columns, Dictionary<string, string>? filters)
        {
            StringBuilder queryBuilder = new StringBuilder();
            queryBuilder.Append($"SELECT {string.Join(", ", columns)} FROM {tableName}");

            if (filters != null && filters.Count > 0)
            {
                queryBuilder.Append(" WHERE ");
                List<string> filterConditions = new List<string>();

                foreach (var filter in filters)
                {
                    string filterColumn = filter.Key;
                    string filterValue = filter.Value;
                    filterConditions.Add($"{filterColumn} LIKE '%{filterValue}%'");
                }

                queryBuilder.Append(string.Join(" AND ", filterConditions));
            }

            try
            {
                using (DatabaseConnection dbConnection = new DatabaseConnection())
                {
                    using (SqlConnection connection = await dbConnection.OpenConnection())
                    {
                        using (SqlCommand command = new SqlCommand(queryBuilder.ToString(), connection))
                        {
                            using (SqlDataAdapter adapter = new SqlDataAdapter(command))
                            {
                                DataTable dataTable = new DataTable();
                                await Task.Run(() => adapter.Fill(dataTable));
                                return dataTable;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        public static async Task<DataTable?> GetUsersTable()
        {
            string query = @"SELECT u.user_id, u.first_name, u.middle_name, u.last_name, u.email, u.username, u.contact_number, u.status, a.access_level
                     FROM tbl_users u
                     LEFT JOIN tbl_user_access a ON u.user_id = a.user_id
                     WHERE u.username <> ''";

            using (DatabaseConnection dbConnection = new DatabaseConnection())
            {
                SqlConnection? connection = await dbConnection.OpenConnection();

                if (connection != null && connection.State == ConnectionState.Open)
                {
                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        using (SqlDataReader reader = await command.ExecuteReaderAsync(CommandBehavior.CloseConnection))
                        {
                            DataTable dataTable = new DataTable();
                            dataTable.Load(reader);
                            return dataTable;
                        }
                    }
                }
            }

            return null;
        }

        public async Task<bool> SavePayrollChanges()
        {
            bool modified = false;

            using DatabaseConnection dbConnection = new();

            SqlConnection? connection = await dbConnection.OpenConnection();

            SqlCommand modifyCommand = new SqlCommand("UPDATE tbl_commissions SET is_valid = 1 WHERE is_valid = 0; UPDATE tbl_overtime SET is_valid = 1 WHERE is_valid = 0; UPDATE tbl_reimbursement SET is_valid = 1 WHERE is_valid = 0; UPDATE tbl_deductions SET is_valid = 1 WHERE is_valid = 0;", connection);

            int rowsAffected = modifyCommand.ExecuteNonQuery();

            if (rowsAffected > 0)
            {
                modified = true;
            }

            CloseConnection();

            return modified;
        }

        public async Task<bool> IsDataExistsAsync(string tableName, string columnName, string value)
        {
            bool result = false;
            try
            {
                using DatabaseConnection connection = new();
                await connection.OpenConnection();

                using SqlCommand? command = connection.CreateCommand();

                if (command == null)
                {
                    return false;
                }

                command.CommandText = $"SELECT COUNT(*) FROM {tableName} WHERE {columnName} = @value";
                command.Parameters.AddWithValue("@value", value);

                object? commandResult = await command.ExecuteScalarAsync();
                int count = commandResult != null ? (int)commandResult : 0;

                if (count > 0)
                {
                    result = true;
                }
            }
            catch (SqlException)
            {
                return false;
            }
            finally
            {
                CloseConnection();
            }

            return result;
        }

        public async Task<bool> InsertData(string tableName, string[] columns, string[] values)
        {
            using DatabaseConnection dbConnection = new();

            try
            {
                SqlConnection? connection = await dbConnection.OpenConnection();

                if(connection == null)
                {
                    return false;
                }

                using SqlCommand command = connection.CreateCommand();

                string query = $"INSERT INTO {tableName} ({string.Join(",", columns)}) " +
                               $"VALUES ({string.Join(",", values.Select((v, i) => $"@value{i}"))});";

                command.CommandText = query;

                for (int i = 0; i < values.Length; i++)
                {
                    command.Parameters.AddWithValue($"@value{i}", values[i]);
                }

                int result = await command.ExecuteNonQueryAsync();

                return result > 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error inserting data: " + ex.Message);
                return false;
            }
            finally
            {
                CloseConnection();
            }
        }

        public async Task<bool> UpdateData(string tableName, string[] columns, string[] values, string whereColumnName, string whereColumnValue)
        {
            using DatabaseConnection dbConnection = new();

            try
            {
                SqlConnection? connection = await dbConnection.OpenConnection();

                if (connection == null)
                {
                    return false;
                }

                using SqlCommand command = connection.CreateCommand();

                string setClause = string.Join(", ", columns.Select((c, i) => $"{c} = @value{i}"));
                string query = $"UPDATE {tableName} SET {setClause} WHERE {whereColumnName} = @whereColumnValue";

                command.CommandText = query;

                for (int i = 0; i < values.Length; i++)
                {
                    command.Parameters.AddWithValue($"@value{i}", values[i]);
                }

                command.Parameters.AddWithValue("@whereColumnValue", whereColumnValue);

                int result = await command.ExecuteNonQueryAsync();

                return result > 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error updating data: " + ex.Message);
                return false;
            }
            finally
            {
                CloseConnection();
            }
        }

        public async Task<string?> InsertOrUpdateData(string tableName, string[] columns, string[] values, string idColumnName, string idValue)
        {
            using DatabaseConnection dbConnection = new();

            try
            {
                SqlConnection? connection = await dbConnection.OpenConnection();

                if (connection == null)
                {
                    return null;
                }

                using SqlCommand command = connection.CreateCommand();

                string query = $"IF EXISTS (SELECT 1 FROM {tableName} WHERE {idColumnName} = @idValue) " +
                               $"UPDATE {tableName} SET {string.Join(", ", columns.Select((c, i) => $"{c} = @value{i}"))} " +
                               $"WHERE {idColumnName} = @idValue " +
                               $"ELSE " +
                               $"INSERT INTO {tableName} ({idColumnName}, {string.Join(",", columns)}) " +
                               $"VALUES (@idValue, {string.Join(",", values.Select((v, i) => $"@value{i}"))})";

                command.CommandText = query;
                command.Parameters.AddWithValue("@idValue", idValue);

                for (int i = 0; i < values.Length; i++)
                {
                    command.Parameters.AddWithValue($"@value{i}", values[i]);
                }

                int result = await command.ExecuteNonQueryAsync();

                return result > 0 ? (result == 1 ? "inserted" : "updated") : null;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error inserting/updating data: " + ex.Message);
                return null;
            }
            finally
            {
                CloseConnection();
            }
        }

        public async Task<bool> AuthenticateUser(string username, string password)
        {
            try
            {
                using (DatabaseConnection connection = new DatabaseConnection())
                {
                    SqlConnection? conn = await connection.OpenConnection();

                    SqlCommand? command = connection.CreateCommand();

                    if (command == null)
                    {
                        return false;
                    }

                    command.CommandText = "SELECT user_id FROM tbl_users WHERE username = @username AND password = @password_hash AND status = 'Enabled'";
                    command.Parameters.AddWithValue("@username", username);
                    command.Parameters.AddWithValue("@password_hash", password);

                    int userID = 0;

                    using (SqlDataReader reader = await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            userID = (int)reader["user_id"];
                        }
                    }

                    if (userID != 0)
                    {
                        // User authentication successful, retrieve user data
                        string query = "SELECT u.first_name, u.middle_name, u.last_name, ua.access_level " +
                                       "FROM tbl_users u " +
                                       "INNER JOIN tbl_user_access ua ON u.user_id = ua.user_id " +
                                       "WHERE u.user_id = @user_id";

                        using (SqlCommand userDataCommand = new SqlCommand(query, conn))
                        {
                            userDataCommand.Parameters.AddWithValue("@user_id", userID);

                            using (SqlDataReader reader = await userDataCommand.ExecuteReaderAsync())
                            {
                                if (await reader.ReadAsync())
                                {
                                    // Set the current user data
                                    CurrentUser.Instance.userID = userID;
                                    CurrentUser.Instance.firstName = (string)reader["first_name"];
                                    CurrentUser.Instance.middleName = (string)reader["middle_name"];
                                    CurrentUser.Instance.lastName = (string)reader["last_name"];
                                    CurrentUser.Instance.accessLevel = (string)reader["access_level"];
                                }
                            }
                        }

                        return true;
                    }
                }

                return false;
            }
            catch (SqlException)
            {
                return false;
            }
            finally
            {
                CloseConnection();
            }
        }

        public async Task<bool> AuthenticationCheck(string tableName, string columnName, string valueToCheck)
        {
            try
            {
                using DatabaseConnection connection = new();

                await connection.OpenConnection();

                SqlCommand? command = connection.CreateCommand();

                if (command == null)
                {
                    MessageBox.Show("null");
                    return false;
                }

                command.CommandText = $"SELECT COUNT(*) FROM {tableName} WHERE {columnName} = @valueToCheck AND (username IS NULL OR username = '') AND (password IS NULL OR password = '')";
                command.Parameters.AddWithValue("@valueToCheck", valueToCheck);

                int count = Convert.ToInt32(command.ExecuteScalar());

                return count > 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                return false;
            }
        }

        public async Task<bool> RegisterUser(string tableName, string[] columns, string[] values, string idColumn, string idValue)
        {
            using DatabaseConnection connection = new();

            await connection.OpenConnection();

            SqlCommand? command = connection.CreateCommand();
            SqlTransaction? transaction = null;

            try
            {
                if (connection == null && command == null)
                {
                    return false;
                }

                transaction = connection.BeginTransaction();
                command.Transaction = transaction;

                // Build UPDATE query for tableName
                StringBuilder updateQuery = new StringBuilder();
                updateQuery.Append("UPDATE ").Append(tableName).Append(" SET ");
                for (int i = 0; i < columns.Length; i++)
                {
                    updateQuery.Append(columns[i]).Append("=@").Append(i);
                    if (i < columns.Length - 1)
                    {
                        updateQuery.Append(", ");
                    }
                    command.Parameters.AddWithValue("@" + i, values[i]);
                }
                updateQuery.Append(" WHERE ").Append(idColumn).Append("=@id");
                command.Parameters.AddWithValue("@id", idValue);

                command.CommandText = updateQuery.ToString();

                int rowsAffected = command.ExecuteNonQuery();

                if (rowsAffected == 0)
                {
                    transaction.Rollback();
                    return false;
                }

                // Query the updated row to get user_id
                string selectQuery = "SELECT user_id FROM " + tableName + " WHERE " + idColumn + "=@id";
                command.CommandText = selectQuery;

                int userId = Convert.ToInt32(command.ExecuteScalar());

                // Insert into tbl_active_users
                string activeUsersQuery = "INSERT INTO tbl_active_users (user_id) VALUES (@user_id)";
                command.CommandText = activeUsersQuery;
                command.Parameters.Clear();
                command.Parameters.AddWithValue("@user_id", userId);

                int activeUsersRowsAffected = command.ExecuteNonQuery();

                if (activeUsersRowsAffected == 0)
                {
                    transaction.Rollback();
                    return false;
                }

                transaction.Commit();
                return true;
            }
            catch (Exception ex)
            {
                transaction?.Rollback();
                MessageBox.Show("Error updating data: " + ex.Message);
                return false;
            }
        }

        public async Task<List<Dictionary<string, object>>?> GetRowsExcluded(string tableName, IEnumerable<string> columnNames, Dictionary<string, object> excludedFilters)
        {
            List<Dictionary<string, object>> rows = new List<Dictionary<string, object>>();

            try
            {
                using (DatabaseConnection connection = new DatabaseConnection())
                {
                    await connection.OpenConnection();

                    if (connection != null)
                    {
                        string query = $"SELECT {string.Join(", ", columnNames)} FROM {tableName}";
                        if (excludedFilters != null && excludedFilters.Count > 0)
                        {
                            query += " WHERE ";
                            int i = 0;
                            foreach (KeyValuePair<string, object> filter in excludedFilters)
                            {
                                if (i > 0)
                                {
                                    query += " AND ";
                                }
                                query += $"{filter.Key} != @{filter.Key}";
                                i++;
                            }
                        }

                        using (SqlCommand? cmd = connection.CreateCommand())
                        {
                            if (cmd == null)
                            {
                                return null;
                            }

                            cmd.CommandText = query;

                            if (excludedFilters != null && excludedFilters.Count > 0)
                            {
                                foreach (KeyValuePair<string, object> filter in excludedFilters)
                                {
                                    cmd.Parameters.AddWithValue($"@{filter.Key}", filter.Value);
                                }
                            }

                            using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                            {
                                while (await reader.ReadAsync())
                                {
                                    Dictionary<string, object> row = new Dictionary<string, object>();
                                    foreach (string columnName in columnNames)
                                    {
                                        row[columnName] = reader[columnName];
                                    }
                                    rows.Add(row);
                                }
                            }
                        }
                    }
                    else
                    {
                        return null;
                    }
                }
                return rows;
            }
            catch
            {
                return null;
            }
        }

        public async Task<Dictionary<string, object>?> GetRow(string tableName, IEnumerable<string> columnNames, string whereColumn, object whereValue)
        {
            Dictionary<string, object> row = new Dictionary<string, object>();

            try
            {
                using (DatabaseConnection connection = new DatabaseConnection())
                {
                    await connection.OpenConnection();

                    if (connection != null)
                    {
                        string query = $"SELECT {string.Join(", ", columnNames)} FROM {tableName} WHERE {whereColumn} = @{whereColumn}";

                        using (SqlCommand? cmd = connection.CreateCommand())
                        {
                            if (cmd == null)
                            {
                                return null;
                            }

                            cmd.CommandText = query;
                            cmd.Parameters.AddWithValue($"@{whereColumn}", whereValue);

                            using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                            {
                                if (await reader.ReadAsync())
                                {
                                    foreach (string columnName in columnNames)
                                    {
                                        row[columnName] = reader[columnName];
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        return null;
                    }
                }
                return row;
            }
            catch
            {
                return null;
            }
        }

        public async Task<bool> InsertDataWithForeignKey(string firstTable, Dictionary<string, object> firstTableValues, string firstTableIdColumn, string secondTable, Dictionary<string, object> secondTableValues, string thirdTable, Dictionary<string, object> thirdTableValues)
        {
            using (DatabaseConnection connection = new DatabaseConnection())
            {
                await connection.OpenConnection();

                using (SqlTransaction transaction = connection.BeginTransaction())
                {
                    try
                    {
                        SqlCommand firstTableInsertCommand = connection.CreateCommand();
                        if (firstTableInsertCommand != null)
                        {
                            firstTableInsertCommand.Transaction = transaction;

                            // Build INSERT query for firstTable
                            StringBuilder firstTableInsertQuery = new StringBuilder();
                            firstTableInsertQuery.Append($"INSERT INTO {firstTable} ({string.Join(",", firstTableValues.Keys)}) OUTPUT INSERTED.{firstTableIdColumn} VALUES ({string.Join(",", firstTableValues.Keys.Select(key => "@" + key))})");
                            firstTableInsertCommand.CommandText = firstTableInsertQuery.ToString();
                            foreach (KeyValuePair<string, object> kvp in firstTableValues)
                            {
                                firstTableInsertCommand.Parameters.AddWithValue($"@{kvp.Key}", kvp.Value);
                            }
                            int generatedId = (int)await firstTableInsertCommand.ExecuteScalarAsync();

                            secondTableValues.Add(firstTableIdColumn, generatedId);
                            SqlCommand secondTableInsertCommand = connection.CreateCommand();
                            secondTableInsertCommand.Transaction = transaction;

                            // Build INSERT query for secondTable
                            StringBuilder secondTableInsertQuery = new StringBuilder();
                            secondTableInsertQuery.Append($"INSERT INTO {secondTable} ({string.Join(",", secondTableValues.Keys)}) VALUES ({string.Join(",", secondTableValues.Keys.Select(key => "@" + key))})");
                            secondTableInsertCommand.CommandText = secondTableInsertQuery.ToString();
                            foreach (KeyValuePair<string, object> kvp in secondTableValues)
                            {
                                secondTableInsertCommand.Parameters.AddWithValue($"@{kvp.Key}", kvp.Value);
                            }
                            await secondTableInsertCommand.ExecuteNonQueryAsync();

                            thirdTableValues.Add(firstTableIdColumn, generatedId);
                            SqlCommand thirdTableInsertCommand = connection.CreateCommand();
                            thirdTableInsertCommand.Transaction = transaction;

                            // Build INSERT query for thirdTable
                            StringBuilder thirdTableInsertQuery = new StringBuilder();
                            thirdTableInsertQuery.Append($"INSERT INTO {thirdTable} ({string.Join(",", thirdTableValues.Keys)}) VALUES ({string.Join(",", thirdTableValues.Keys.Select(key => "@" + key))})");
                            thirdTableInsertCommand.CommandText = thirdTableInsertQuery.ToString();
                            foreach (KeyValuePair<string, object> kvp in thirdTableValues)
                            {
                                thirdTableInsertCommand.Parameters.AddWithValue($"@{kvp.Key}", kvp.Value);
                            }
                            await thirdTableInsertCommand.ExecuteNonQueryAsync();

                            transaction.Commit();
                            return true;
                        }
                        else
                        {
                            Console.WriteLine("Failed to create SqlCommand for the first table.");
                            transaction.Rollback();
                            return false;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                        transaction.Rollback();
                        return false;
                    }
                }
            }
        }

        public async Task<string> GetValue(string tableName, string columnNameToGet, string whereColumnName, string whereColumnValue)
        {
            try
            {
                using DatabaseConnection connection = new();
                await connection.OpenConnection();

                using SqlCommand? command = connection.CreateCommand();

                if (command == null)
                {

                    return string.Empty;
                }

                command.CommandText = $"SELECT {columnNameToGet} FROM {tableName} WHERE {whereColumnName} = @whereColumnValue";
                command.Parameters.AddWithValue("@whereColumnValue", whereColumnValue);

                object? commandResult = await command.ExecuteScalarAsync();
                string value = commandResult?.ToString() ?? string.Empty;
                return value;
            }
            catch (SqlException)
            {
                return string.Empty;
            }
            finally
            {
                CloseConnection();
            }
        }

        public async Task <List<User>?> GetUsers()
        {
            List<User> users = new List<User>();

            using DatabaseConnection connection = new();

            try
            {
                using (SqlConnection? conn = await connection.OpenConnection())
                {
                    if (conn != null)
                    {
                        using (SqlCommand cmd = new SqlCommand("SELECT u.user_id, u.last_name, u.first_name " +
                                                               "FROM tbl_users u " +
                                                               "LEFT JOIN tbl_user_access a ON u.user_id = a.user_id " +
                                                               "WHERE u.username IS NOT NULL " +
                                                               "AND u.username <> ''", conn))
                        {
                            using (SqlDataReader reader = cmd.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    User user = new User();
                                    user.userID = reader.GetInt32(reader.GetOrdinal("user_id"));
                                    string? firstName = reader["first_name"].ToString();
                                    string? lastName = reader["last_name"].ToString();

                                    users.Add(user);
                                }
                            }
                        }
                    }
                }
                return users;
            }
            catch (Exception e)
            {
                return null;
            }
        }

        public async Task<DataTable> GetPayrollData()
        {

            using DatabaseConnection? conn = new();

            using (SqlConnection? connection = await conn.OpenConnection())
            {
                DataTable payrollData = new DataTable();

                try
                {

                    // Get user IDs and names from tbl_users
                    SqlCommand userCommand = new SqlCommand("SELECT u.user_id, u.last_name, u.first_name, u.middle_name " +
                                                            "FROM tbl_users u " +
                                                            "LEFT JOIN tbl_user_access a ON u.user_id = a.user_id " +
                                                            "WHERE u.username IS NOT NULL " +
                                                            "AND u.username <> '' " +
                                                            "AND u.password IS NOT NULL " +
                                                            "AND u.password <> '' " +
                                                            "AND a.access_level <> 'ADMIN'", connection);
                    SqlDataAdapter userAdapter = new SqlDataAdapter(userCommand);
                    DataTable userTable = new DataTable();
                    userAdapter.Fill(userTable);

                    // Calculate hours worked for each user
                    SqlCommand hoursCommand = new SqlCommand("SELECT user_id, SUM(hours_worked) AS hours_worked FROM tbl_work_hours WHERE issued = 0 GROUP BY user_id", connection);
                    SqlDataAdapter hoursAdapter = new SqlDataAdapter(hoursCommand);
                    DataTable hoursTable = new DataTable();
                    hoursAdapter.Fill(hoursTable);

                    // Calculate commissions for each user
                    SqlCommand commisionCommand = new SqlCommand("SELECT user_id, SUM(commission_amount) AS total_commision FROM tbl_commissions WHERE issued = 0 GROUP BY user_id", connection);
                    SqlDataAdapter commisionAdapter = new SqlDataAdapter(commisionCommand);
                    DataTable commisionTable = new DataTable();
                    commisionAdapter.Fill(commisionTable);

                    // Calculate commissions for each user
                    SqlCommand overtimeCommand = new SqlCommand("SELECT user_id, SUM(overtime) AS total_overtime FROM tbl_overtime WHERE issued = 0 GROUP BY user_id", connection);
                    SqlDataAdapter overtimeAdapter = new SqlDataAdapter(overtimeCommand);
                    DataTable overtimeTable = new DataTable();
                    overtimeAdapter.Fill(overtimeTable);

                    // Calculate commissions for each user
                    SqlCommand reimbursementCommand = new SqlCommand("SELECT user_id, SUM(amount) AS total_reimbursement FROM tbl_reimbursement WHERE issued = 0 GROUP BY user_id", connection);
                    SqlDataAdapter reimbursementAdapter = new SqlDataAdapter(reimbursementCommand);
                    DataTable reimbursementTable = new DataTable();
                    reimbursementAdapter.Fill(reimbursementTable);

                    // Calculate commissions for each user
                    SqlCommand deductionCommand = new SqlCommand("SELECT user_id, SUM(amount) AS total_deductions FROM tbl_deductions WHERE issued = 0 GROUP BY user_id", connection);
                    SqlDataAdapter deductionAdapter = new SqlDataAdapter(deductionCommand);
                    DataTable deductionTable = new DataTable();
                    deductionAdapter.Fill(deductionTable);

                    // Merge user, hours, and commission tables to get final payroll data
                    payrollData.Columns.Add("user_id");
                    payrollData.Columns.Add("name");
                    payrollData.Columns.Add("hours_worked");
                    payrollData.Columns.Add("additional_earnings");
                    payrollData.Columns.Add("commission");
                    payrollData.Columns.Add("gross_pay");
                    payrollData.Columns.Add("overtime");
                    payrollData.Columns.Add("regular_pay");
                    payrollData.Columns.Add("overtime_pay");
                    payrollData.Columns.Add("reimbursement");
                    payrollData.Columns.Add("deductions");

                    foreach (DataRow userRow in userTable.Rows)
                    {
                        string userId = userRow["user_id"].ToString();
                        string lastName = userRow["last_name"].ToString();
                        string firstName = userRow["first_name"].ToString();
                        string middleName = userRow["middle_name"].ToString();

                        // Construct name string
                        string name = lastName + ", " + firstName;

                        if (middleName != "N/A" && middleName.Length > 0)
                        {
                            name += " " + middleName.Substring(0, 1) + ".";
                        }

                        // Get hours worked for user
                        DataRow[] hoursRows = hoursTable.Select("user_id = '" + userId + "'");
                        double hoursWorked = 0.0;

                        if (hoursRows.Length > 0 && hoursRows[0]["hours_worked"] != DBNull.Value)
                        {
                            hoursWorked = Convert.ToDouble(hoursRows[0]["hours_worked"]);
                        }
                        else
                        {
                            hoursWorked = 0.0;
                        }

                        DataRow[] overtimeRows = overtimeTable.Select("user_id = '" + userId + "'");
                        double overtime = 0.0;

                        if (overtimeRows.Length > 0)
                        {
                            overtime = Convert.ToDouble(overtimeRows[0]["total_overtime"]);
                        }

                        // Get commission for user
                        DataRow[] commissionRows = commisionTable.Select("user_id = '" + userId + "'");
                        double commission = 0.0;

                        if (commissionRows.Length > 0)
                        {
                            commission = Convert.ToDouble(commissionRows[0]["total_commision"]);
                        }

                        DataRow[] reimbursementnRows = reimbursementTable.Select("user_id = '" + userId + "'");
                        double reimbursement = 0.0;

                        if (reimbursementnRows.Length > 0)
                        {
                            reimbursement = Convert.ToDouble(reimbursementnRows[0]["total_reimbursement"]);
                        }

                        DataRow[] deductionRows = deductionTable.Select("user_id = '" + userId + "'");
                        double deduction = 0.0;

                        if (deductionRows.Length > 0)
                        {
                            deduction = Convert.ToDouble(deductionRows[0]["total_deductions"]);
                        }

                        // Calculate gross pay for user
                        double hourlyRate = 0.0;
                        SqlCommand wageCommand = new SqlCommand("SELECT hourly_rate FROM tbl_wage WHERE user_id = '" + userId + "'", connection);
                        object hourlyRateObj = wageCommand.ExecuteScalar();

                        if (hourlyRateObj != null && !Convert.IsDBNull(hourlyRateObj))
                        {
                            hourlyRate = Convert.ToDouble(hourlyRateObj);
                        }

                        double regularPay = (hoursWorked - overtime) * hourlyRate;
                        double overtimePay = overtime * 80;

                        double grossPay = ((hoursWorked - overtime) * hourlyRate) + commission + reimbursement + (overtime * 80) - deduction;
                        //double grossPay = hourlyRate * hoursWorked + commission + (overtime * hourlyRate) + reimbursement;

                        // Add data row to payroll data table
                        DataRow payrollRow = payrollData.NewRow();
                        payrollRow["user_id"] = userId;
                        payrollRow["name"] = name;
                        payrollRow["hours_worked"] = hoursWorked;
                        payrollRow["commission"] = 0;
                        payrollRow["gross_pay"] = string.Format("{0:N2}", grossPay);
                        payrollRow["additional_earnings"] = string.Format("{0:N2}", commission);
                        payrollRow["regular_pay"] = string.Format("{0:N2}", regularPay);
                        payrollRow["overtime_pay"] = string.Format("{0:N2}", overtimePay);
                        payrollRow["overtime"] = overtime;
                        payrollRow["reimbursement"] = string.Format("{0:N2}", reimbursement);
                        payrollRow["deductions"] = string.Format("{0:N2}", deduction);
                        payrollData.Rows.Add(payrollRow);
                    }
                }

                catch (SqlException ex)
                {
                    MessageBox.Show("Error retrieving payroll data: " + ex.Message);
                }

                return payrollData;
            }
        }

        public async void UpdateWorkHoursAndActiveUsers(int? userId, DateTime endTime)
        {
            using DatabaseConnection? conn = new();

            using (SqlConnection? connection = await conn.OpenConnection())
            {
                string query = @"
                UPDATE tbl_work_hours
                SET end_time = @endTime, hours_worked = DATEDIFF(minute, start_time, @endTime) / 60.0
                WHERE user_id = @userId AND end_time IS NULL;
            
                UPDATE tbl_active_users
                SET logout_time = @endTime
                WHERE user_id = @userId AND logout_time IS NULL;";

                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@userId", userId);
                    command.Parameters.AddWithValue("@endTime", endTime);

                    command.ExecuteNonQuery();
                }
            }
        }

        public async void InsertOrUpdateWorkHoursAndActiveUsers(int? userId, DateTime loginTime)
        {
            using DatabaseConnection? conn = new();

            using (SqlConnection? connection = await conn.OpenConnection())
            {
                string query = @"
                        INSERT INTO tbl_work_hours (user_id, start_time)
                        SELECT @userId, @loginTime
                        WHERE NOT EXISTS (
                            SELECT 1 FROM tbl_work_hours WHERE user_id = @userId AND end_time IS NULL
                        );

                        MERGE tbl_active_users AS target
                        USING (VALUES (@userId, @loginTime)) AS source(user_id, login_time)
                        ON target.user_id = source.user_id
                        WHEN MATCHED THEN
                            UPDATE SET target.login_time = source.login_time, target.logout_time = NULL
                        WHEN NOT MATCHED THEN
                        INSERT (user_id, login_time)
                        VALUES (source.user_id, source.login_time);";

                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@userId", userId);
                    command.Parameters.AddWithValue("@loginTime", loginTime);

                    command.ExecuteNonQuery();
                }
            }
        }

        public async Task<Dictionary<string, double>> GetReimbursementsForUser(string userId)
        {
            Dictionary<string, double> remimbursementData = new Dictionary<string, double>();
            using DatabaseConnection? conn = new();

            using (SqlConnection? connection = await conn.OpenConnection())
            {
                string query = "SELECT description, amount FROM tbl_reimbursement WHERE user_id = @UserId AND issued = 0";

                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@UserId", userId);

                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string description = reader["description"].ToString();
                            double amount = Convert.ToDouble(reader["amount"]);

                            if (remimbursementData.ContainsKey(description))
                            {
                                remimbursementData[description] += amount;
                            }
                            else
                            {
                                remimbursementData.Add(description, amount);
                            }
                        }
                    }
                }
            }

            return remimbursementData;
        }

        public async Task <Dictionary<string, double>> GetCommissionDataForUser(string userId)
        {
            Dictionary<string, double> commissionData = new Dictionary<string, double>();

            using DatabaseConnection? conn = new();

            using (SqlConnection? connection = await conn.OpenConnection())
            {
                string query = "SELECT commission_name, commission_amount FROM tbl_commissions WHERE user_id = @UserId AND issued = 0";

                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@UserId", userId);

                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string commissionType = reader["commission_name"].ToString();
                            double commissionAmount = Convert.ToDouble(reader["commission_amount"]);

                            if (commissionData.ContainsKey(commissionType))
                            {
                                commissionData[commissionType] += commissionAmount;
                            }
                            else
                            {
                                commissionData.Add(commissionType, commissionAmount);
                            }
                        }
                    }
                }
            }

            return commissionData;
        }

        public async Task<DataTable> GetUsersDataTable(string condition)
        {
            string query = $@"SELECT u.user_id, u.first_name, u.middle_name, u.last_name, u.email, u.username, u.contact_number, u.status, a.access_level
                      FROM tbl_users u
                      INNER JOIN tbl_active_users au ON u.user_id = au.user_id
                      LEFT JOIN tbl_user_access a ON u.user_id = a.user_id
                      WHERE {condition}
                      GROUP BY u.user_id, u.first_name, u.middle_name, u.last_name, u.email, u.username, u.contact_number, u.status, a.access_level";

            using DatabaseConnection? conn = new();

            using (SqlConnection? connection = await conn.OpenConnection())
            {
                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    using (SqlDataReader reader = await command.ExecuteReaderAsync())
                    {
                        try
                        {
                            DataTable dataTable = new DataTable();
                            dataTable.Load(reader);
                            return dataTable;
                        }
                        catch (Exception ex)
                        {
                            return null;
                        }
                    }
                }
            }
        }

        public async Task<(int active, int inactive, int disabled)> GetUserCounts()
        {
            int activeCount = 0;
            int inactiveCount = 0;
            int disabledCount = 0;

            using DatabaseConnection? conn = new();

            using (SqlConnection? connection = await conn.OpenConnection())
            {
                string activeQuery = "SELECT COUNT(*) FROM tbl_active_users au INNER JOIN tbl_users u ON au.user_id = u.user_id WHERE u.status = 'Enabled' AND au.login_time IS NOT NULL AND au.logout_time IS NULL";
                SqlCommand activeCommand = new SqlCommand(activeQuery, connection);
                activeCount = (int)activeCommand.ExecuteScalar();

                string inactiveQuery = "SELECT COUNT(*) FROM tbl_active_users au INNER JOIN tbl_users u ON au.user_id = u.user_id WHERE u.status = 'Enabled' AND (au.login_time IS NOT NULL AND au.logout_time IS NOT NULL OR au.login_time IS NULL AND au.logout_time IS NULL)";
                SqlCommand inactiveCommand = new SqlCommand(inactiveQuery, connection);
                inactiveCount = (int)inactiveCommand.ExecuteScalar();

                string disabledQuery = "SELECT COUNT(*) FROM tbl_users WHERE status = 'Disabled'";
                SqlCommand disabledCommand = new SqlCommand(disabledQuery, connection);
                disabledCount = (int)disabledCommand.ExecuteScalar();
            }

            return (activeCount, inactiveCount, disabledCount);
        }

        public async Task<(int inStock, int lowStock, int outOfStock)> GetProductsCount()
        {
            int inStockCount = 0;
            int lowStockCount = 0;
            int outOfStockCount = 0;

            using DatabaseConnection? conn = new();

            using (SqlConnection? connection = await conn.OpenConnection())
            {
                string inStockQuery = "SELECT COUNT(*) FROM tbl_products WHERE status = 'IN-STOCK'";
                SqlCommand inStockCommand = new SqlCommand(inStockQuery, connection);
                inStockCount = (int)inStockCommand.ExecuteScalar();

                string lowStockQuery = "SELECT COUNT(*) FROM tbl_products WHERE status = 'LOW-STOCK'";
                SqlCommand lowStockCommand = new SqlCommand(lowStockQuery, connection);
                lowStockCount = (int)lowStockCommand.ExecuteScalar();

                string outOfStockQuery = "SELECT COUNT(*) FROM tbl_products WHERE status = 'OUT OF STOCK'";
                SqlCommand outOfStockCommand = new SqlCommand(outOfStockQuery, connection);
                outOfStockCount = (int)outOfStockCommand.ExecuteScalar();
            }

            return (inStockCount, lowStockCount, outOfStockCount);
        }

        public async Task  <bool> CheckIfLogged(string tableName, string columnId, string columnIdValue, string columnValue)
        {
            using DatabaseConnection? conn = new();

            using (SqlConnection? connection = await conn.OpenConnection())
            {
                string query = $"SELECT COUNT(*) FROM {tableName} WHERE {columnId} = @{columnId} AND {columnValue} IS NULL";
                SqlCommand command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue($"@{columnId}", columnIdValue);

                int count = (int)command.ExecuteScalar();

                return count > 0;
            }
        }

        public async Task <bool> CheckIfExists(string tableName, string columnId, string columnIdValue, string columnValue, string columnValueToCheck)
        {
            using DatabaseConnection conn = new();

            using (SqlConnection? connection = await conn.OpenConnection())
            {
                string query = $"SELECT COUNT(*) FROM {tableName} WHERE {columnId} = @{columnId} AND {columnValue} = @{columnValue}";
                SqlCommand command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue($"@{columnId}", columnIdValue);
                command.Parameters.AddWithValue($"@{columnValue}", columnValueToCheck);

                int count = (int)command.ExecuteScalar();

                return count > 0;
            }
        }
    }
}
