using System;
using System.Data;
using UnityWinForms.System.Windows.Forms;
using Mono.Data.Sqlite;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityWinForms.Controls;
using UnityWinForms.Examples;
using UnityWinForms.Examples.Panels;
using UnityWinForms.System.Drawing;

namespace Orbital.Test
{
    public class DatabaseTest : MonoBehaviour
    {
        [FilePath] public string path; 
        public string commandText;
        public string insertCommandText;
        
        [Button]
        void Start() 
        {
            DataSet dataSet = new DataSet();
            using (SqliteConnection connection = new SqliteConnection("Data Source=" + path))
            {
                connection.Open();
                using (SqliteCommand command = new SqliteCommand(commandText, connection))
                {
                    SqliteDataAdapter adapter = new SqliteDataAdapter(command);
                    adapter.Fill(dataSet);
                    adapter.Dispose();
                }
            }
//dataSet.Tables[0]
            Form form = new ViewForm(dataSet.Tables[0]);
            form.Show();
        }
        
        [Button]
        private void InsertCommand()
        {
            using (SqliteConnection connection = new SqliteConnection("Data Source=" + path))
            {
                connection.Open();
                using (SqliteCommand command = new SqliteCommand(commandText, connection))
                {
                    var answer = command.ExecuteScalar();
                    if(answer != null) Debug.Log(answer.ToString());
                    /*SqliteDataAdapter adapter = new SqliteDataAdapter(command);
                    adapter.Update()
                    adapter.Dispose();*/
                }
            }
        }
    }
    

    public class ViewForm : Form
    {

        public ViewForm(DataTable table)
        {
            InitializeComponent();
            for (int i = 0; i < table.Columns.Count; i++)
            {
                Debug.Log("Column: " + table.Columns[i].ColumnName);

                _tableView.Columns.Add(table.Columns[i].ColumnName, table.Columns[i].ColumnName);
                _tableView.Columns[i].Width = 100;
            }
            for (int i = 0; i < table.Rows.Count + 1; i++)
            {
                _tableView.Rows.Add();
            }
            for (int i = 0; i < table.Columns.Count; i++)
            {
                for (int j = 0; j < table.Rows.Count; j++)
                {
                    string content = table.Rows[j][i].ToString();
                    Debug.Log(content);
                    _tableView.Rows[j].ItemsControls[i] = new Label(){Text = content};
                }
            }
            _tableView.Refresh();
        }

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            _tableView = this.Create<TableView>(8, 158);
            _tableView.Size = new Size(360, 240);
            Controls.Add(_tableView);
            SuspendLayout();

            BackColor = SystemColors.Control;
            ClientSize = new Size(365, 245);
            Location = new Point(15, 15);
            Name = "ViewForm";
            ResumeLayout(false);
            Disposed += OnDisposed;
        }


        private void OnDisposed(object sender, EventArgs e)
        {
        }

        private TableView _tableView;
    }
}