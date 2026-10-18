using System.Reflection;

MessageBox.Show(
    $"TEST GAME\n\n" +
    $"Executable: {Environment.ProcessPath}\n" +
    $"Version: {Assembly.GetExecutingAssembly().GetName().Version}",
    "Unity Test Replacement",
    MessageBoxButtons.OK,
    MessageBoxIcon.Information);
