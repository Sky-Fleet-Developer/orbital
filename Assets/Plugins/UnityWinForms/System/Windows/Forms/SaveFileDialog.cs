namespace UnityWinForms.System.Windows.Forms
{
    public class SaveFileDialog : FileDialog
    {
        public SaveFileDialog()
        {
            Text = "Save as";

            buttonOk.Text = "Save";
        }
    }
}
