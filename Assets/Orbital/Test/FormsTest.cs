using System.Collections;
using System.Collections.Generic;
using System.Windows.Forms;
using UnityEngine;

    public class FormsTest : MonoBehaviour
    {
        void Start()
        {
            TestForm newForm = new TestForm();
            newForm.Show();
        }
    }

    public class TestForm : Form
    {
        public TestForm()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.button1 = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // button1
            // 
            this.button1.Location = new System.Drawing.Point(12, 12);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(94, 33);
            this.button1.TabIndex = 0;
            this.button1.Text = "button1";
            this.button1.UseVisualStyleBackColor = true;
            // 
            // TestForm
            // 
            this.ClientSize = new System.Drawing.Size(284, 261);
            this.Controls.Add(this.button1);
            this.Name = "TestForm";
            this.ResumeLayout(false);
        }

        private System.Windows.Forms.Button button1;
    }
