﻿using DataWarehouseAutomation;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace TEAM
{
    public partial class FormManageMetadata : FormBase
    {
        Form_Alert _alert;
        Form_Alert _alertValidation;
        Form_Alert _generatedScripts;
        Form_Alert _generatedJsonInterface;

        //Getting the DataTable to bind to something
        private BindingSource _bindingSourceTableMetadata = new BindingSource();
        private BindingSource _bindingSourceAttributeMetadata = new BindingSource();
        private BindingSource _bindingSourcePhysicalModelMetadata = new BindingSource();

        public FormManageMetadata()
        {
            InitializeComponent();
        }
        
        public FormManageMetadata(FormMain parent) : base(parent)
        {
            InitializeComponent();

            radiobuttonNoVersionChange.Checked = true;
            MetadataParameters.ValidationIssues = 0;
            MetadataParameters.ValidationRunning = false;

            labelHubCount.Text = "0 Core Business Concepts";
            labelSatCount.Text = "0 Context entities";
            labelLnkCount.Text = "0 Relationships";
            labelLsatCount.Text = "0 Relationship context entities";

            radiobuttonNoVersionChange.Checked = true;

            // Retrieve the version from the repository database
            //var connOmd = new SqlConnection {ConnectionString = TeamConfigurationSettings.MetadataConnection.CreateConnectionString(false) };
            //var selectedVersion = GetMaxVersionId(connOmd);

            string versionFileName = GlobalParameters.CorePath + GlobalParameters.VersionFileName + GlobalParameters.JsonExtension;
            if (!File.Exists(versionFileName))
            {
                EnvironmentVersion.CreateNewVersionListFile(versionFileName, GlobalParameters.WorkingEnvironment);
            }

            var selectedVersion = EnvironmentVersion.GetMaxVersionForEnvironment(GlobalParameters.WorkingEnvironment);
            
            
            // Set the version in memory
            GlobalParameters.CurrentVersionId = selectedVersion.Item1;
            GlobalParameters.HighestVersionId = selectedVersion.Item1; // On startup, the highest version is the same as the current version
            JsonHandling.FileConfiguration.jsonVersionExtension = @"_v" + selectedVersion.Item1 + ".json";

            trackBarVersioning.Maximum = selectedVersion.Item1;
            trackBarVersioning.TickFrequency = EnvironmentVersion.GetTotalVersionCount(GlobalParameters.WorkingEnvironment);

            //Make sure the version is displayed
            var versionMajorMinor = EnvironmentVersion.GetMajorMinorForVersionId(GlobalParameters.WorkingEnvironment, selectedVersion.Item1);
            var majorVersion = versionMajorMinor.Item2;
            var minorVersion = versionMajorMinor.Item3;

            trackBarVersioning.Value = selectedVersion.Item1;
            labelVersion.Text = majorVersion + "." + minorVersion;

            //  Load the grids from the repository
            richTextBoxInformation.Clear();
            PopulateTableMappingGridWithVersion(selectedVersion.Item1);
            PopulateAttributeGridWithVersion(selectedVersion.Item1);
            PopulatePhysicalModelGridWithVersion(selectedVersion.Item1);

            richTextBoxInformation.Text +="The metadata for version " + majorVersion + "." + minorVersion + " has been loaded.";
            
            ContentCounter();

            // Make sure the validation information is available in this form
            try
            {
                var validationFile = GlobalParameters.ConfigurationPath + GlobalParameters.ValidationFileName + '_' + GlobalParameters.WorkingEnvironment + GlobalParameters.FileExtension;

                // If the config file does not exist yet, create it by calling the EnvironmentConfiguration Class
                if (!File.Exists(validationFile))
                {
                    LocalTeamEnvironmentConfiguration.CreateDummyValidationFile(validationFile);
                }

                // Load the validation settings file using the paths retrieved from the application root contents (configuration path)
                LocalTeamEnvironmentConfiguration.LoadValidationFile(validationFile);

                richTextBoxInformation.Text += "\r\nThe validation file " + validationFile + " has been loaded.";
            }
            catch (Exception)
            {
                // ignored
            }

            #region CheckedListBox for reverse engineering
            checkedListBoxReverseEngineeringAreas.CheckOnClick = true;
            checkedListBoxReverseEngineeringAreas.ValueMember = "Key";
            checkedListBoxReverseEngineeringAreas.DisplayMember = "Value";

            // Load the checkboxes for the reverse-engineering tab
            foreach (var connection in TeamConfigurationSettings.ConnectionDictionary)
            {
                checkedListBoxReverseEngineeringAreas.Items.Add(new KeyValuePair<TeamConnection, string>(connection.Value, connection.Value.ConnectionKey));
            }
            #endregion
        }

        /// <summary>
        /// Sets the ToolTip text for cells in the DataGridView (hover over).
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void DataGridViewTableMetadata_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            // Retrieve the full row for the selected cell
            DataGridViewRow selectedRow = dataGridViewTableMetadata.Rows[e.RowIndex];

            if (selectedRow.IsNewRow == false)
            {
                var sourceMapping = selectedRow.DataGridView.Rows[e.RowIndex].Cells[(int) TableMappingMetadataColumns.SourceTable].Value.ToString();
                var targetMapping = selectedRow.DataGridView.Rows[e.RowIndex].Cells[(int) TableMappingMetadataColumns.TargetTable].Value.ToString();

                var loadVector = "";
                MetadataHandling.TableTypes tableType = MetadataHandling.TableTypes.Unknown;
                DataGridViewCell cell = null;

                if (e.Value != null && sourceMapping != null && targetMapping != null)
                {
                    loadVector = MetadataHandling.GetLoadVector(sourceMapping, targetMapping, TeamConfigurationSettings);
                }

                // Assert table type for Source column, retrieve the specific cell value for the hover-over.
                if (e.ColumnIndex == dataGridViewTableMetadata.Columns[(int) TableMappingMetadataColumns.SourceTable].Index &&
                    e.Value != null)
                {
                    cell = dataGridViewTableMetadata.Rows[e.RowIndex].Cells[e.ColumnIndex];
                    tableType = MetadataHandling.GetTableType(e.Value.ToString(), "", TeamConfigurationSettings);
                }
                // Assert table type for the Target column, , retrieve the specific cell value for the hover-over.
                else if ((e.ColumnIndex ==
                          dataGridViewTableMetadata.Columns[(int) TableMappingMetadataColumns.TargetTable].Index &&
                          e.Value != null))
                {
                    cell = dataGridViewTableMetadata.Rows[e.RowIndex].Cells[e.ColumnIndex];
                    tableType = MetadataHandling.GetTableType(e.Value.ToString(), selectedRow.DataGridView.Rows[e.RowIndex]
                            .Cells[(int) TableMappingMetadataColumns.BusinessKeyDefinition].Value.ToString(), TeamConfigurationSettings);
                }
                else
                {
                    // Do nothing
                }

                if (cell != null)
                {
                    cell.ToolTipText = "The table " + e.Value + " has been evaluated as a " + tableType + " object." +
                                       "\n" + "The direction of loading is " + loadVector + ".";
                }
            }
        }

        private void DataGridViewPhysicalModelMetadataKeyDown(object sender, KeyEventArgs e)
        {
            try
            {
                if (e.Modifiers == Keys.Control)
                {
                    switch (e.KeyCode)
                    {
                        case Keys.V:
                            PasteClipboardPhysicalModelMetadata();
                            break;
                    }
                }
            }
            catch (Exception)
            {
                MessageBox.Show("Pasting into the data grid has failed", "Copy/Paste", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void PasteClipboardPhysicalModelMetadata()
        {
            try
            {
                string s = Clipboard.GetText();
                string[] lines = s.Split('\n');

                int iRow = dataGridViewPhysicalModelMetadata.CurrentCell.RowIndex;
                int iCol = dataGridViewPhysicalModelMetadata.CurrentCell.ColumnIndex;
                DataGridViewCell oCell;
                if (iRow + lines.Length > dataGridViewPhysicalModelMetadata.Rows.Count - 1)
                {
                    bool bFlag = false;
                    foreach (string sEmpty in lines)
                    {
                        if (sEmpty == "")
                        {
                            bFlag = true;
                        }
                    }

                    int iNewRows = iRow + lines.Length - dataGridViewPhysicalModelMetadata.Rows.Count;
                    if (iNewRows > 0)
                    {
                        if (bFlag)
                            dataGridViewPhysicalModelMetadata.Rows.Add(iNewRows);
                        else
                            dataGridViewPhysicalModelMetadata.Rows.Add(iNewRows + 1);
                    }
                    else
                        dataGridViewPhysicalModelMetadata.Rows.Add(iNewRows + 1);
                }
                foreach (string line in lines)
                {
                    if (iRow < dataGridViewPhysicalModelMetadata.RowCount && line.Length > 0)
                    {
                        string[] sCells = line.Split('\t');
                        for (int i = 0; i < sCells.GetLength(0); ++i)
                        {
                            if (iCol + i < dataGridViewPhysicalModelMetadata.ColumnCount)
                            {
                                oCell = dataGridViewPhysicalModelMetadata[iCol + i, iRow];
                                oCell.Value = Convert.ChangeType(sCells[i].Replace("\r", ""), oCell.ValueType);
                            }
                            else
                            {
                                break;
                            }
                        }
                        iRow++;
                    }
                    else
                    {
                        break;
                    }
                }
                //Clipboard.Clear();
            }
            catch (FormatException)
            {
                MessageBox.Show("There is an issue with the data format for this cell!");
            }
        }

        private void PopulatePhysicalModelGridWithVersion(int versionId)
        {
            string jsonPhysicalModelFile = JsonHandling.FileConfiguration.physicalModelJsonFileName();

            //Check if the file exists, otherwise create a dummy / empty file   
            if (!File.Exists(jsonPhysicalModelFile))
                {
                    richTextBoxInformation.AppendText("No Json file was found, so a new empty one was created.\r\n");
                    JsonHandling.CreateDummyJsonFile(GlobalParameters.JsonModelMetadataFileName);
                }

                // Load the file, convert it to a DataTable and bind it to the source
                List<PhysicalModelMetadataJson> jsonArray = JsonConvert.DeserializeObject<List<PhysicalModelMetadataJson>>(File.ReadAllText(jsonPhysicalModelFile));

                DataTable dt = Utility.ConvertToDataTable(jsonArray);

                //Make sure the changes are seen as committed, so that changes can be detected later on.
                dt.AcceptChanges();

                SetTeamDataTableProperties.SetPhysicalModelDataTableColumns(dt);

                _bindingSourcePhysicalModelMetadata.DataSource = dt;

                if (jsonArray != null)
                {
                    // Set the column header names.
                    dataGridViewPhysicalModelMetadata.DataSource = _bindingSourcePhysicalModelMetadata;
                    dataGridViewPhysicalModelMetadata.ColumnHeadersVisible = true;
                    dataGridViewPhysicalModelMetadata.Columns[0].Visible = false;
                    dataGridViewPhysicalModelMetadata.Columns[1].Visible = false;

                    dataGridViewPhysicalModelMetadata.Columns[0].HeaderText = "Hash Key"; //Key column
                    dataGridViewPhysicalModelMetadata.Columns[1].HeaderText = "Version ID"; //Key column
                    dataGridViewPhysicalModelMetadata.Columns[2].HeaderText = "Database Name"; //Key column
                    dataGridViewPhysicalModelMetadata.Columns[3].HeaderText = "Schema Name"; //Key column
                    dataGridViewPhysicalModelMetadata.Columns[4].HeaderText = "Table Name"; //Key column
                    dataGridViewPhysicalModelMetadata.Columns[5].HeaderText = "Column Name"; //Key column
                    dataGridViewPhysicalModelMetadata.Columns[6].HeaderText = "Data Type";
                    dataGridViewPhysicalModelMetadata.Columns[7].HeaderText = "Length";
                    dataGridViewPhysicalModelMetadata.Columns[8].HeaderText = "Precision";
                    dataGridViewPhysicalModelMetadata.Columns[9].HeaderText = "Position";
                    dataGridViewPhysicalModelMetadata.Columns[10].HeaderText = "Primary Key";
                    dataGridViewPhysicalModelMetadata.Columns[11].HeaderText = "Multi-Active";
                }

                richTextBoxInformation.AppendText("The file " + jsonPhysicalModelFile + " was loaded.\r\n");
            
            GridAutoLayoutPhysicalModelMetadata();
        }

        /// <summary>
        /// Populate the Table Mapping DataGrid from an existing Json file.
        /// </summary>
        /// <param name="versionId"></param>
        private void PopulateTableMappingGridWithVersion(int versionId)
        {
            // Check if the file exists, otherwise create a dummy / empty file   
            string jsonTableMappingFile = JsonHandling.FileConfiguration.tableMappingJsonFileName();

            if (!File.Exists(jsonTableMappingFile))
            {
                richTextBoxInformation.AppendText($"No Json file was found, so a new empty one was created: {jsonTableMappingFile}.\r\n");
                JsonHandling.CreateDummyJsonFile(GlobalParameters.JsonTableMappingFileName);
            }

            // Load the file into memory (datatable and json list)
            TableMapping.GetTableMapping(jsonTableMappingFile);

            // Handle unknown combobox values, by setting them to empty.
            var localConnectionKeyList = LocalTeamConnection.TeamConnectionKeyList(TeamConfigurationSettings.ConnectionDictionary);
            List<string> userFeedbackList = new List<string>();
            foreach (DataRow row in TableMapping.TableMappingDataTable.Rows)
            {
                var comboBoxValueSource = row[(int)TableMappingMetadataColumns.SourceConnection].ToString();
                var comboBoxValueTarget= row[(int)TableMappingMetadataColumns.TargetConnection].ToString();

                
                if (!localConnectionKeyList.Contains(comboBoxValueSource))
                {
                    if (!userFeedbackList.Contains(comboBoxValueSource))
                    {
                        userFeedbackList.Add(comboBoxValueSource);
                    }

                    row[(int) TableMappingMetadataColumns.SourceConnection] = DBNull.Value;
                }

                if (!localConnectionKeyList.Contains(comboBoxValueTarget))
                {
                    if (!userFeedbackList.Contains(comboBoxValueTarget))
                    {
                        userFeedbackList.Add(comboBoxValueTarget);
                    }

                    row[(int)TableMappingMetadataColumns.TargetConnection] = DBNull.Value;
                }
            }

            // Provide user feedback is any of the connections have been invalidated.
            if (userFeedbackList.Count > 0)
            {
                foreach (string issue in userFeedbackList)
                {
                    richTextBoxInformation.AppendText($"The connection {issue} found in the metadata file does not seem to exist in TEAM. The value has been defaulted, but not saved yet.\r\n");
                }
            }
            
            //Make sure the changes are seen as committed, so that changes can be detected later on.
            TableMapping.TableMappingDataTable.AcceptChanges(); 

            // Order by Source Table, Integration_Area table, Business Key Attribute.
            TableMapping.SetTableDataTableColumns();
            TableMapping.SetTableDataTableSorting();

            _bindingSourceTableMetadata.DataSource = TableMapping.TableMappingDataTable;

            // Set the column header names etc. for the data grid view.
            dataGridViewTableMetadata.DataSource = _bindingSourceTableMetadata;
            
            richTextBoxInformation.AppendText($"The file {jsonTableMappingFile} was loaded.\r\n");

            // Resize the grid
            GridAutoLayoutTableMappingMetadata();
        }

        /// <summary>
        /// Populates the data grid directly from a database or an existing JSON file
        /// </summary>
        /// <param name="versionId"></param>
        private void PopulateAttributeGridWithVersion(int versionId)
        {
            string jsonAttributeMappingFile = JsonHandling.FileConfiguration.attributeMappingJsonFileName();

            //Check if the file exists, otherwise create a dummy / empty file   
            if (!File.Exists(jsonAttributeMappingFile))
            {
                richTextBoxInformation.AppendText("No attribute mapping Json file was found, so a new empty one was created.\r\n");
                JsonHandling.CreateDummyJsonFile(GlobalParameters.JsonAttributeMappingFileName);
            }

            // Load the file, convert it to a DataTable and bind it to the source
            List<AttributeMappingJson> jsonArray = JsonConvert.DeserializeObject<List<AttributeMappingJson>>(File.ReadAllText(jsonAttributeMappingFile));
            DataTable dt = Utility.ConvertToDataTable(jsonArray);
            dt.AcceptChanges(); //Make sure the changes are seen as committed, so that changes can be detected later on
            SetTeamDataTableProperties.SetAttributeDataTableColumns(dt);

            _bindingSourceAttributeMetadata.DataSource = dt;

            if (jsonArray != null)
            {
                // Set the column header names.
                dataGridViewAttributeMetadata.DataSource = _bindingSourceAttributeMetadata;
                dataGridViewAttributeMetadata.ColumnHeadersVisible = true;
                dataGridViewAttributeMetadata.Columns[0].Visible = false;
                dataGridViewAttributeMetadata.Columns[1].Visible = false;
                dataGridViewAttributeMetadata.Columns[6].ReadOnly = false;

                dataGridViewAttributeMetadata.Columns[0].HeaderText = "Hash Key";
                dataGridViewAttributeMetadata.Columns[1].HeaderText = "Version ID";
                dataGridViewAttributeMetadata.Columns[2].HeaderText = "Source Table";
                dataGridViewAttributeMetadata.Columns[3].HeaderText = "Source Column";
                dataGridViewAttributeMetadata.Columns[4].HeaderText = "Target Table";
                dataGridViewAttributeMetadata.Columns[5].HeaderText = "Target Column";
                dataGridViewAttributeMetadata.Columns[6].HeaderText = "Notes";
            }

            richTextBoxInformation.AppendText("The file " + jsonAttributeMappingFile + " was loaded.\r\n");

            // Resize the grid
            GridAutoLayoutAttributeMetadata();
        }


        private DialogResult STAShowDialog(FileDialog dialog)
        {
            var state = new DialogState {FileDialog = dialog};
            var t = new Thread(state.ThreadProcShowDialog);
            t.SetApartmentState(ApartmentState.STA);

            t.Start();
            t.Join();

            return state.DialogResult;
        }

        public class DialogState
        {
            public DialogResult DialogResult;
            public FileDialog FileDialog;

            public void ThreadProcShowDialog()
            {
                DialogResult = FileDialog.ShowDialog();
            }
        }

        private void GridAutoLayout()
        {
            if (checkBoxResizeDataGrid.Checked == false)
                return;
    
            GridAutoLayoutTableMappingMetadata();
            GridAutoLayoutAttributeMetadata();
            GridAutoLayoutPhysicalModelMetadata();
        }

        private void GridAutoLayoutTableMappingMetadata()
        {
            if (checkBoxResizeDataGrid.Checked == false)
                return;

            //Table Mapping metadata grid - set the autosize based on all cells for each column
            //for (var i = 0; i < dataGridViewTableMetadata.Columns.Count - 1; i++)
            //{
            //    dataGridViewTableMetadata.Columns[i].AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
            //}
            //if (dataGridViewTableMetadata.Columns.Count > 0)
            //{
            //    dataGridViewTableMetadata.Columns[dataGridViewTableMetadata.Columns.Count - 1].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            //}
            //// Table Mapping metadata grid - disable the auto size again (to enable manual resizing)
            //for (var i = 0; i < dataGridViewTableMetadata.Columns.Count - 1; i++)
            //{
            //    int columnWidth = dataGridViewTableMetadata.Columns[i].Width;
            //    dataGridViewTableMetadata.Columns[i].AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
            //    dataGridViewTableMetadata.Columns[i].Width = columnWidth;
            //}

            dataGridViewTableMetadata.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells;
            //dataGridViewLoadPatternCollection.Columns[3].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            dataGridViewTableMetadata.Columns[dataGridViewTableMetadata.ColumnCount - 1].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

            // Disable the auto size again (to enable manual resizing).
            for (var i = 0; i < dataGridViewTableMetadata.Columns.Count - 1; i++)
            {

                dataGridViewTableMetadata.Columns[i].AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
                dataGridViewTableMetadata.Columns[i].Width = dataGridViewTableMetadata.Columns[i].GetPreferredWidth(DataGridViewAutoSizeColumnMode.AllCells, true);
            }

        }

        private void GridAutoLayoutAttributeMetadata()
        {
            if (checkBoxResizeDataGrid.Checked == false)
                return;

            ////Set the autosize based on all cells for each column
            //for (var i = 0; i < dataGridViewAttributeMetadata.Columns.Count - 1; i++)
            //{
            //    dataGridViewAttributeMetadata.Columns[i].AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
            //}
            //if (dataGridViewAttributeMetadata.Columns.Count > 0)
            //{
            //    dataGridViewAttributeMetadata.Columns[dataGridViewAttributeMetadata.Columns.Count - 1].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            //}

            //// Disable the auto size again (to enable manual resizing)
            //for (var i = 0; i < dataGridViewAttributeMetadata.Columns.Count - 1; i++)
            //{
            //    int columnWidth = dataGridViewAttributeMetadata.Columns[i].Width;
            //    dataGridViewAttributeMetadata.Columns[i].AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
            //    dataGridViewAttributeMetadata.Columns[i].Width = columnWidth;
            //}

            dataGridViewAttributeMetadata.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells;
            //dataGridViewLoadPatternCollection.Columns[3].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            dataGridViewAttributeMetadata.Columns[dataGridViewAttributeMetadata.ColumnCount - 1].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

            // Disable the auto size again (to enable manual resizing).
            for (var i = 0; i < dataGridViewAttributeMetadata.Columns.Count - 1; i++)
            {

                dataGridViewAttributeMetadata.Columns[i].AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
                dataGridViewAttributeMetadata.Columns[i].Width = dataGridViewAttributeMetadata.Columns[i].GetPreferredWidth(DataGridViewAutoSizeColumnMode.AllCells, true);
            }
        }

        private void GridAutoLayoutPhysicalModelMetadata()
        {
            if (checkBoxResizeDataGrid.Checked == false)
                return;

            ////Physical model metadata grid - set the autosize based on all cells for each column
            //for (var i = 0; i < dataGridViewPhysicalModelMetadata.Columns.Count - 1; i++)
            //{
            //    dataGridViewPhysicalModelMetadata.Columns[i].AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
            //}
            //if (dataGridViewPhysicalModelMetadata.Columns.Count > 0)
            //{
            //    dataGridViewPhysicalModelMetadata.Columns[dataGridViewPhysicalModelMetadata.Columns.Count - 1].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            //}
            //// Disable the auto size again (to enable manual resizing)
            //for (var i = 0; i < dataGridViewPhysicalModelMetadata.Columns.Count - 1; i++)
            //{
            //    int columnWidth = dataGridViewPhysicalModelMetadata.Columns[i].Width;
            //    dataGridViewPhysicalModelMetadata.Columns[i].AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
            //    dataGridViewPhysicalModelMetadata.Columns[i].Width = columnWidth;
            //}

            dataGridViewPhysicalModelMetadata.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells;
            //dataGridViewLoadPatternCollection.Columns[3].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            dataGridViewPhysicalModelMetadata.Columns[dataGridViewPhysicalModelMetadata.ColumnCount - 1].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

            // Disable the auto size again (to enable manual resizing).
            for (var i = 0; i < dataGridViewPhysicalModelMetadata.Columns.Count - 1; i++)
            {

                dataGridViewPhysicalModelMetadata.Columns[i].AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
                dataGridViewPhysicalModelMetadata.Columns[i].Width = dataGridViewPhysicalModelMetadata.Columns[i].GetPreferredWidth(DataGridViewAutoSizeColumnMode.AllCells, true);
            }
        }

        private void ContentCounter()
        {
            int gridViewRows = dataGridViewTableMetadata.RowCount;
            var counter = 0;

            var hubSet = new HashSet<string>();
            var satSet = new HashSet<string>();
            var lnkSet = new HashSet<string>();
            var lsatSet = new HashSet<string>();

            foreach (DataGridViewRow row in dataGridViewTableMetadata.Rows)
            {
                var integrationTable = row.Cells[(int)TableMappingMetadataColumns.TargetTable].Value;

                if (gridViewRows != counter + 1 && integrationTable.ToString().Length>3)
                {
                    if (integrationTable.ToString().Substring(0, 4) == "HUB_")
                    {
                        if (!hubSet.Contains(integrationTable.ToString()))
                        {
                            hubSet.Add(integrationTable.ToString());
                        }
                    }
                    else if (integrationTable.ToString().Substring(0, 4) == "SAT_")
                    {
                        if (!satSet.Contains(integrationTable.ToString()))
                        {
                            satSet.Add(integrationTable.ToString());
                        }
                    }
                    else if (integrationTable.ToString().Substring(0, 5) == "LSAT_")
                    {
                        if (!lsatSet.Contains(integrationTable.ToString()))
                        {
                            lsatSet.Add(integrationTable.ToString());
                        }
                    }
                    else if (integrationTable.ToString().Substring(0, 4) == "LNK_")
                    {
                        if (!lnkSet.Contains(integrationTable.ToString()))
                        {
                            lnkSet.Add(integrationTable.ToString());
                        }
                    }
                }
                counter++;
            }

            labelHubCount.Text = hubSet.Count + " Core Business Concepts";
            labelSatCount.Text = satSet.Count + " Context Entities";
            labelLnkCount.Text = lnkSet.Count + " Natural Business Relationships";
            labelLsatCount.Text = lsatSet.Count + " Relationship Context Entities";
        }

        private void closeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void manageValidationRulesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var t = new Thread(ThreadProcValidation);
            t.SetApartmentState(ApartmentState.STA);
            t.Start();
        }

        private void CloseValidationForm(object sender, FormClosedEventArgs e)
        {
            _myValidationForm = null;
        }  

        // Threads starting for other (sub) forms
        private FormManageValidation _myValidationForm;
        public void ThreadProcValidation()
        {
            if (_myValidationForm == null)
            {
                _myValidationForm = new FormManageValidation(this);
                _myValidationForm.Show();

                Application.Run();
            }

            else
            {
                if (_myValidationForm.InvokeRequired)
                {
                    // Thread Error
                    _myValidationForm.Invoke((MethodInvoker)delegate { _myValidationForm.Close(); });
                    _myValidationForm.FormClosed += CloseValidationForm;

                    _myValidationForm = new FormManageValidation(this);
                    _myValidationForm.Show();
                    Application.Run();
                }
                else
                {
                    // No invoke required - same thread
                    _myValidationForm.FormClosed += CloseValidationForm;

                    _myValidationForm = new FormManageValidation(this);
                    _myValidationForm.Show();
                    Application.Run();
                }

            }
        }

        private void trackBarVersioning_ValueChanged(object sender, EventArgs e)
        {
            richTextBoxInformation.Clear();
            JsonHandling.FileConfiguration.jsonVersionExtension = @"_v" + trackBarVersioning.Value + ".json";
            GlobalParameters.CurrentVersionId = trackBarVersioning.Value;
            
            PopulateTableMappingGridWithVersion(trackBarVersioning.Value);
            PopulateAttributeGridWithVersion(trackBarVersioning.Value);
            PopulatePhysicalModelGridWithVersion(trackBarVersioning.Value);

            var versionMajorMinor = EnvironmentVersion.GetMajorMinorForVersionId(GlobalParameters.WorkingEnvironment,trackBarVersioning.Value);

            labelVersion.Text = versionMajorMinor.Item2 + "." + versionMajorMinor.Item3;

            //richTextBoxInformation.Text = "The metadata for version " + majorVersion + "." + minorVersion + " has been loaded.";
            ContentCounter();
        }


        /// <summary>
        ///   Clicking the 'save' button.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonSaveMetadata_Click(object sender, EventArgs e)
        {
            richTextBoxInformation.Clear();

            // Check if the current version is the maximum version. At this stage updates on earlier versions are not supported (and cause a NULL reference exception)
            var highestVersion = GlobalParameters.HighestVersionId;
            var currentVersion = GlobalParameters.CurrentVersionId;

            if (currentVersion < highestVersion)
            {
                richTextBoxInformation.Text += "Cannot save the metadata changes because these are applied to an earlier version. Only updates to the latest or newer version are supported in TEAM.";
            }
            else
            {
                // Create a data table containing the changes, to check if there are changes made to begin with
                var dataTableTableMappingChanges = ((DataTable) _bindingSourceTableMetadata.DataSource).GetChanges();
                var dataTableAttributeMappingChanges = ((DataTable) _bindingSourceAttributeMetadata.DataSource).GetChanges();
                var dataTablePhysicalModelChanges = ((DataTable) _bindingSourcePhysicalModelMetadata.DataSource).GetChanges();

                // Check if there are any rows available in the grid view, and if changes have been detected at all
                if (
                    dataGridViewTableMetadata.RowCount > 0 && dataTableTableMappingChanges != null &&
                    dataTableTableMappingChanges.Rows.Count > 0 ||
                    dataGridViewAttributeMetadata.RowCount > 0 && dataTableAttributeMappingChanges != null &&
                    dataTableAttributeMappingChanges.Rows.Count > 0 ||
                    dataGridViewPhysicalModelMetadata.RowCount > 0 && dataTablePhysicalModelChanges != null &&
                    dataTablePhysicalModelChanges.Rows.Count > 0
                )
                {
                    //Create new version, or retain the old one, depending on selection (version radiobuttons)

                    // Capture the 'old ' current version in case the UI needs updating
                    var oldVersionId = trackBarVersioning.Value;

                    //Retrieve the current version, or create a new one
                    int versionId = CreateOrRetrieveVersion();

                    //Commit the save of the metadata, one for each grid
                    try
                    {
                        SaveTableMappingMetadataJson(versionId, dataTableTableMappingChanges);
                    }
                    catch (Exception exception)
                    {
                        richTextBoxInformation.Text +=
                            "The Table Mapping metadata wasn't saved. There are errors saving the metadata version. The reported error is: " +
                            exception;
                    }

                    try
                    {
                        SaveAttributeMappingMetadata(versionId, dataTableAttributeMappingChanges);
                    }
                    catch (Exception exception)
                    {
                        richTextBoxInformation.Text +=
                            "The Attribute Mapping metadata wasn't saved. There are errors saving the metadata version. The reported error is: " +
                            exception;
                    }
                    try
                    {
                        SaveModelPhysicalModelMetadata(versionId, dataTablePhysicalModelChanges);
                    }
                    catch (Exception exception)
                    {
                        richTextBoxInformation.Text +=
                            "The Physical Model metadata wasn't saved. There are errors saving the metadata version. The reported error is: " +
                            exception;
                    }

                    //Load the grids from the repository after being updated
                    PopulateTableMappingGridWithVersion(versionId);
                    PopulateAttributeGridWithVersion(versionId);
                    PopulatePhysicalModelGridWithVersion(versionId);

                    //Refresh the UI to display the newly created version
                    if (oldVersionId != versionId)
                    {
                        var maxVersion = EnvironmentVersion.GetMaxVersionForEnvironment(GlobalParameters.WorkingEnvironment);

                        trackBarVersioning.Maximum = maxVersion.Item1;
                        trackBarVersioning.TickFrequency = EnvironmentVersion.GetTotalVersionCount(GlobalParameters.WorkingEnvironment);
                        trackBarVersioning.Value = maxVersion.Item1;
                    }
                }
                else
                {
                    richTextBoxInformation.Text += "There is no metadata to save!";
                }
            }
        }

        /// <summary>
        /// Verifies the version checkbox (major or minor) and creates new version instance. If 'no change' is checked this will return the current version Id.
        /// </summary>
        /// <returns></returns>
        private int CreateOrRetrieveVersion()
        {
            if (!radiobuttonNoVersionChange.Checked)
            {
                //If nothing is checked, just retrieve and return the current version
                var versionKeyValuePair = EnvironmentVersion.GetMaxVersionForEnvironment(GlobalParameters.WorkingEnvironment);
                var majorVersion = versionKeyValuePair.Item2;
                var minorVersion = versionKeyValuePair.Item3;

                //Increase the major version, if required
                if (radiobuttonMajorRelease.Checked)
                {
                    try
                    {
                        //Creates a new version
                        majorVersion++;
                        minorVersion = 0;
                        EnvironmentVersion.AddNewVersionToList(GlobalParameters.WorkingEnvironment,majorVersion, 0);
                        EnvironmentVersion.SaveVersionList(GlobalParameters.CorePath+GlobalParameters.VersionFileName+GlobalParameters.JsonExtension);
                    }
                    catch (Exception ex)
                    {
                        richTextBoxInformation.Text += "An issue occured when saving a new version: " + ex;
                    }
                }

                //Increase the minor version, if required
                if (radioButtonMinorRelease.Checked)
                {
                    try
                    {
                        //Creates a new version
                        minorVersion++;
                        EnvironmentVersion.AddNewVersionToList(GlobalParameters.WorkingEnvironment, majorVersion, minorVersion);
                        EnvironmentVersion.SaveVersionList(GlobalParameters.CorePath + GlobalParameters.VersionFileName + GlobalParameters.JsonExtension);
                    }
                    catch (Exception ex)
                    {
                        richTextBoxInformation.Text += "An issue occured when saving a new version: " + ex;
                    }
                }
            }

            //Retrieve the current version (again, may have changed).
            var newVersionKeyValuePair = EnvironmentVersion.GetMaxVersionForEnvironment(GlobalParameters.WorkingEnvironment);

            //Make sure the correct version is added to the global parameters
            GlobalParameters.CurrentVersionId = newVersionKeyValuePair.Item1;
            GlobalParameters.HighestVersionId = newVersionKeyValuePair.Item1;
            JsonHandling.FileConfiguration.jsonVersionExtension = @"_v" + newVersionKeyValuePair.Item1 + ".json";

            labelVersion.Text = newVersionKeyValuePair.Item2 + "." + newVersionKeyValuePair.Item3;

            return newVersionKeyValuePair.Item1;
        }

        /// <summary>
        /// Creates a new snapshot of the Physical Model metadata to a Json target repository, with the versionId as input parameter.
        /// This method creates a new version in the repository for the physical model (TEAM_Model.json file).
        /// </summary>
        /// <param name="versionId"></param>
        internal void CreateNewPhysicalModelMetadataVersionJson(int versionId)
        {
            // Update the version extension for the file.
            JsonHandling.FileConfiguration.jsonVersionExtension = @"_v" + versionId + ".json";

            // Create a JArray so segments can be added easily from the data table.
            var jsonModelMappingFull = new JArray();

            try
            {
                foreach (DataGridViewRow row in dataGridViewPhysicalModelMetadata.Rows)
                {
                    if (!row.IsNewRow)
                    {
                        string databaseName = "";
                        string schemaName = "";
                        string tableName = "";
                        string columnName = "";
                        string dataType = "";
                        string maxLength = "0";
                        string numericPrecision = "0";
                        string ordinalPosition = "0";
                        string primaryKeyIndicator = "";
                        string multiActiveIndicator = "";

                        if (row.Cells[2].Value != DBNull.Value)
                        {
                            databaseName = (string)row.Cells[2].Value;
                        }

                        if (row.Cells[3].Value != DBNull.Value)
                        {
                            schemaName = (string)row.Cells[3].Value;
                        }

                        if (row.Cells[4].Value != DBNull.Value)
                        {
                            tableName = (string) row.Cells[4].Value;
                        }

                        if (row.Cells[5].Value != DBNull.Value)
                        {
                            columnName = (string) row.Cells[5].Value;
                        }

                        if (row.Cells[6].Value != DBNull.Value)
                        {
                            dataType = (string) row.Cells[6].Value;
                        }

                        if (row.Cells[7].Value != DBNull.Value)
                        {
                            maxLength = (string) row.Cells[7].Value;
                        }

                        if (row.Cells[8].Value != DBNull.Value)
                        {
                            numericPrecision = (string) row.Cells[8].Value;
                        }

                        if (row.Cells[9].Value != DBNull.Value)
                        {
                            ordinalPosition = (string) row.Cells[9].Value;
                        }

                        if (row.Cells[10].Value != DBNull.Value)
                        {
                            primaryKeyIndicator = (string) row.Cells[10].Value;
                        }

                        if (row.Cells[11].Value != DBNull.Value)
                        {
                            multiActiveIndicator = (string) row.Cells[11].Value;
                        }

                        string[] inputHashValue = new string[] { versionId.ToString(), tableName, columnName};
                        var hashKey = Utility.CreateMd5(inputHashValue, Utility.SandingElement);
                      
                        JObject newJsonSegment = new JObject(
                            new JProperty("versionAttributeHash", hashKey),
                            new JProperty("versionId", versionId),
                            new JProperty("databaseName", databaseName),
                            new JProperty("schemaName", schemaName),
                            new JProperty("tableName", tableName),
                            new JProperty("columnName", columnName),
                            new JProperty("dataType", dataType),
                            new JProperty("characterMaximumLength", maxLength),
                            new JProperty("numericPrecision", numericPrecision),
                            new JProperty("ordinalPosition", ordinalPosition),
                            new JProperty("primaryKeyIndicator", primaryKeyIndicator),
                            new JProperty("multiActiveIndicator", multiActiveIndicator)
                        );

                        jsonModelMappingFull.Add(newJsonSegment);
                    }
                }

            }
            catch (Exception ex)
            {
                GlobalParameters.TeamEventLog.Add(Event.CreateNewEvent(EventTypes.Warning, $"A snapshot of the physical model was attempted to be created as a Json array, but this did not succeed. The message is {ex}."));
            }

            try
            {
                //Generate a unique key using a hash
                string output = JsonConvert.SerializeObject(jsonModelMappingFull, Formatting.Indented);
                string outputFileName = JsonHandling.FileConfiguration.physicalModelJsonFileName();
                File.WriteAllText(outputFileName, output);
            }
            catch (JsonReaderException ex)
            {
                richTextBoxInformation.Text += "There were issues inserting the new Json version file for the Physical Model.\r\n" + ex;
            }

        }

        /// <summary>
        /// Creates a new snapshot of the Table Mapping metadata for a JSON repository, with the versionId as input parameter. A new file will created for the provided version Id.
        /// </summary>
        /// <param name="versionId"></param>
        internal void CreateNewTableMappingMetadataVersionJson(int versionId)
        {
            JsonHandling.FileConfiguration.jsonVersionExtension = @"_v" + versionId + ".json";

            // Create a JArray so segments can be added easily from the data table
            var jsonTableMappingFull = new JArray();

            foreach (DataGridViewRow row in dataGridViewTableMetadata.Rows)
            {
                if (!row.IsNewRow)
                {
                    try
                    {
                        var sourceTable = "";
                        var sourceConnectionKey = "";
                        var targetTable = "";
                        var targetConnectionKey = "";
                        var businessKeyDefinition = "";
                        var drivingKeyDefinition = "";
                        var filterCriterion = "";
                        bool generateIndicator = true;

                        if (row.Cells[TableMappingMetadataColumns.SourceTable.ToString()].Value != DBNull.Value)
                        {
                            sourceTable = (string) row.Cells[TableMappingMetadataColumns.SourceTable.ToString()].Value;
                        }

                        // Source Connection
                        if (row.Cells[TableMappingMetadataColumns.SourceConnection.ToString()].Value != DBNull.Value)
                        {
                            sourceConnectionKey =
                                (string) row.Cells[TableMappingMetadataColumns.SourceConnection.ToString()].Value;
                        }

                        if (row.Cells[TableMappingMetadataColumns.TargetTable.ToString()].Value != DBNull.Value)
                        {
                            targetTable = (string) row.Cells[TableMappingMetadataColumns.TargetTable.ToString()].Value;
                        }

                        // Target Connection
                        if (row.Cells[TableMappingMetadataColumns.TargetConnection.ToString()].Value != DBNull.Value)
                        {
                            targetConnectionKey =
                                (string) row.Cells[TableMappingMetadataColumns.TargetConnection.ToString()].Value;
                        }

                        if (row.Cells[TableMappingMetadataColumns.BusinessKeyDefinition.ToString()].Value != DBNull.Value)
                        {
                            businessKeyDefinition =
                                (string) row.Cells[TableMappingMetadataColumns.BusinessKeyDefinition.ToString()].Value;
                            //businessKeyDefinition = businessKeyDefinition.Replace("'", "''");  //Double quotes for composites
                        }

                        if (row.Cells[TableMappingMetadataColumns.DrivingKeyDefinition.ToString()].Value != DBNull.Value)
                        {
                            drivingKeyDefinition =
                                (string) row.Cells[TableMappingMetadataColumns.DrivingKeyDefinition.ToString()].Value;
                            //drivingKeyDefinition = drivingKeyDefinition.Replace("'", "''"); //Double quotes for composites
                        }

                        if (row.Cells[TableMappingMetadataColumns.FilterCriterion.ToString()].Value != DBNull.Value)
                        {
                            filterCriterion = (string) row.Cells[TableMappingMetadataColumns.FilterCriterion.ToString()].Value;
                            //filterCriterion = filterCriterion.Replace("'", "''"); //Double quotes for composites
                        }

                        if (row.Cells[TableMappingMetadataColumns.Enabled.ToString()].Value != DBNull.Value)
                        {
                            generateIndicator = (bool) row.Cells[TableMappingMetadataColumns.Enabled.ToString()].Value;
                            //generateIndicator = generateIndicator.Replace("'", "''"); //Double quotes for composites
                        }

                        string[] inputHashValue = new string[]
                        {
                            versionId.ToString(), sourceTable, targetTable, businessKeyDefinition, drivingKeyDefinition,
                            filterCriterion
                        };
                        var hashKey = Utility.CreateMd5(inputHashValue, Utility.SandingElement);

                        // Convert it into a JArray so segments can be added easily
                        JObject newJsonSegment = new JObject(
                            new JProperty("enabledIndicator", generateIndicator),
                            new JProperty("tableMappingHash", hashKey),
                            new JProperty("versionId", versionId),
                            new JProperty("sourceTable", sourceTable),
                            new JProperty("sourceConnectionKey", sourceConnectionKey),
                            new JProperty("targetTable", targetTable),
                            new JProperty("targetConnectionKey", targetConnectionKey),
                            new JProperty("businessKeyDefinition", businessKeyDefinition),
                            new JProperty("drivingKeyDefinition", drivingKeyDefinition),
                            new JProperty("filterCriteria", filterCriterion)
                        );

                        jsonTableMappingFull.Add(newJsonSegment);
                    }
                    catch (Exception ex)
                    {
                        // TBD
                    }
                }
            }

            try
            {
                //Generate a unique key using a hash
                string output = JsonConvert.SerializeObject(jsonTableMappingFull, Formatting.Indented);
                string outputFileName = JsonHandling.FileConfiguration.tableMappingJsonFileName();

                File.WriteAllText(outputFileName, output);
            }
            catch (JsonReaderException ex)
            {
                richTextBoxInformation.Text += "There were issues inserting the new Json version file for the Table Mapping.\r\n" + ex;
            }
        }

        /// <summary>
        /// Creates a new snapshot of the Attribute Mapping metadata for a JSON repository, with the versionId as input parameter. A new file will created for the provided version Id.
        /// </summary>
        /// <param name="versionId"></param>
        internal void CreateNewAttributeMappingMetadataVersionJson(int versionId)
        {
            JsonHandling.FileConfiguration.jsonVersionExtension = @"_v" + versionId + ".json";

            // Create a JArray so segments can be added easily from the datatable
            var jsonAttributeMappingFull = new JArray();

            foreach (DataGridViewRow row in dataGridViewAttributeMetadata.Rows)
            {
                if (!row.IsNewRow)
                {
                    var stagingTable = "";
                    var stagingColumn = "";
                    var integrationTable = "";
                    var integrationColumn = "";
                    var notes = "";

                    if (row.Cells[2].Value != DBNull.Value)
                    {
                        stagingTable = (string)row.Cells[2].Value;
                    }

                    if (row.Cells[3].Value != DBNull.Value)
                    {
                        stagingColumn = (string)row.Cells[3].Value;
                    }

                    if (row.Cells[4].Value != DBNull.Value)
                    {
                        integrationTable = (string)row.Cells[4].Value;
                    }

                    if (row.Cells[5].Value != DBNull.Value)
                    {
                        integrationColumn = (string)row.Cells[5].Value;
                    }

                    if (row.Cells[6].Value != DBNull.Value)
                    {
                        notes = (string)row.Cells[6].Value;
                    }


                    string[] inputHashValue = new string[] { versionId.ToString(), stagingTable, stagingColumn, integrationTable, integrationColumn, notes };
                    var hashKey = Utility.CreateMd5(inputHashValue, Utility.SandingElement);

                   
                    JObject newJsonSegment = new JObject(
                        new JProperty("attributeMappingHash", hashKey),
                        new JProperty("versionId", versionId),
                        new JProperty("sourceTable", stagingTable),
                        new JProperty("sourceAttribute", stagingColumn),
                        new JProperty("targetTable", integrationTable),
                        new JProperty("targetAttribute", integrationColumn),
                        new JProperty("notes", notes)
                    );

                    jsonAttributeMappingFull.Add(newJsonSegment);
                }
            }

            // Execute the statement, if the repository is JSON
            try
            {
                //Generate a unique key using a hash
                string output = JsonConvert.SerializeObject(jsonAttributeMappingFull, Formatting.Indented);
                string outputFileName = JsonHandling.FileConfiguration.attributeMappingJsonFileName();
                File.WriteAllText(outputFileName, output);
            }
            catch (JsonReaderException ex)
            {
                richTextBoxInformation.Text += "There were issues inserting the new JSON version file for the Attribute Mapping.\r\n" + ex;
            }

        }

        private void SaveTableMappingMetadataJson(int versionId, DataTable dataTableChanges)
        {
            if (JsonHandling.FileConfiguration.newFileTableMapping == "true")
            {
                JsonHandling.RemoveExistingJsonFile(GlobalParameters.JsonTableMappingFileName + @"_v" + GlobalParameters.CurrentVersionId + ".json");
                JsonHandling.CreatePlaceholderJsonFile(GlobalParameters.JsonTableMappingFileName);
                JsonHandling.FileConfiguration.newFileTableMapping = "false";
            }

            //If no change radio buttons are selected this means either minor or major version is checked, so a full new snapshot will be created of everything.
            if (!radiobuttonNoVersionChange.Checked)
            {
                CreateNewTableMappingMetadataVersionJson(versionId);
            }

            //... otherwise an in-place update to the existing version is done (insert / update / delete)
            else
            {
                if (dataTableChanges != null && (dataTableChanges.Rows.Count > 0)) //Double-check if there are any changes made at all
                {
                    foreach (DataRow row in dataTableChanges.Rows) //Start looping through the changes
                    {
                        #region Changed rows
                        //Changed rows
                        if ((row.RowState & DataRowState.Modified) != 0)
                        {
                            //Grab the attributes into local variables
                            string hashKey = (string)row[TableMappingMetadataColumns.HashKey.ToString()];
                            int versionKey = (int)row[TableMappingMetadataColumns.VersionId.ToString()];
                            var stagingTable = "";
                            var sourceConnectionKey= "";
                            var targetConnectionKey = "";
                            var integrationTable = "";
                            var businessKeyDefinition = "";
                            var drivingKeyDefinition = "";
                            var filterCriterion = "";
                            bool generateIndicator = true;

                            if (row[TableMappingMetadataColumns.Enabled.ToString()] != DBNull.Value)
                            {
                                generateIndicator = (bool)row[TableMappingMetadataColumns.Enabled.ToString()];
                            }

                            if (row[TableMappingMetadataColumns.SourceTable.ToString()] != DBNull.Value)
                            {
                                stagingTable = (string)row[TableMappingMetadataColumns.SourceTable.ToString()];
                            }

                            if (row[TableMappingMetadataColumns.SourceConnection.ToString()] != DBNull.Value)
                            {
                                sourceConnectionKey = (string)row[TableMappingMetadataColumns.SourceConnection.ToString()];
                            }

                            if (row[TableMappingMetadataColumns.TargetTable.ToString()] != DBNull.Value)
                            {
                                integrationTable = (string)row[TableMappingMetadataColumns.TargetTable.ToString()];
                            }

                            if (row[TableMappingMetadataColumns.TargetConnection.ToString()] != DBNull.Value)
                            {
                                targetConnectionKey = (string)row[TableMappingMetadataColumns.TargetConnection.ToString()];
                            }

                            if (row[TableMappingMetadataColumns.BusinessKeyDefinition.ToString()] != DBNull.Value)
                            {
                                businessKeyDefinition = (string)row[TableMappingMetadataColumns.BusinessKeyDefinition.ToString()];
                            }
                            if (row[TableMappingMetadataColumns.DrivingKeyDefinition.ToString()] != DBNull.Value)
                            {
                                drivingKeyDefinition = (string)row[TableMappingMetadataColumns.DrivingKeyDefinition.ToString()];
                            }
                            if (row[TableMappingMetadataColumns.FilterCriterion.ToString()] != DBNull.Value)
                            {
                                filterCriterion = (string)row[TableMappingMetadataColumns.FilterCriterion.ToString()];
                            }

                            //Read the file in memory
                            string inputFileName = JsonHandling.FileConfiguration.tableMappingJsonFileName();
                            TableMappingJson[] jsonArray = JsonConvert.DeserializeObject<TableMappingJson[]>(File.ReadAllText(inputFileName));

                            //Retrieves the json segment in the file for the given hash returns value or NULL
                            var jsonHash = jsonArray.FirstOrDefault(obj => obj.tableMappingHash == hashKey);

                            if (jsonHash.tableMappingHash == "")
                            {
                                richTextBoxInformation.Text += "The correct segment in the Json file was not found.\r\n";
                            }
                            else
                            {
                                // Update the values in the JSON segment
                                jsonHash.enabledIndicator = generateIndicator;
                                jsonHash.sourceTable = stagingTable;
                                jsonHash.sourceConnectionKey = sourceConnectionKey;
                                jsonHash.targetTable = integrationTable;
                                jsonHash.targetConnectionKey = targetConnectionKey;
                                jsonHash.businessKeyDefinition = businessKeyDefinition;
                                jsonHash.drivingKeyDefinition = drivingKeyDefinition;
                                jsonHash.filterCriteria = filterCriterion;
                            }

                            string output = JsonConvert.SerializeObject(jsonArray, Formatting.Indented);

                            try
                            {
                                // Write the updated JSON file to disk. NOTE - DOES NOT ALWAYS WORK WHEN FILE IS OPEN IN NOTEPAD AND DOES NOT RAISE EXCEPTION
                                string outputFileName = JsonHandling.FileConfiguration.tableMappingJsonFileName();
                                File.WriteAllText(outputFileName, output);
                            }
                            catch (JsonReaderException ex)
                            {
                                richTextBoxInformation.Text += "There were issues saving the Json update to disk.\r\n" + ex;
                            }
                            //var bla2 = jsonArray.Any(obj => obj.tableMappingHash == "1029C102DE45D40066210E426B605885"); // Returns true if any is found
                        }
                        #endregion

                        #region Inserted rows
                        //Inserted rows
                        if ((row.RowState & DataRowState.Added) != 0)
                        {
                            var sourceTable = "";
                            var sourceConnectionKey = "";
                            var targetTable = "";
                            var targetConnectionKey = "";
                            var businessKeyDefinition = "";
                            var drivingKeyDefinition = "";
                            var filterCriterion = "";
                            bool generateIndicator = true;

                            if (row[(int)TableMappingMetadataColumns.Enabled] != DBNull.Value)
                            {
                                generateIndicator = (bool)row[(int)TableMappingMetadataColumns.Enabled];
                                //generateIndicator = generateIndicator.Replace("'", "''");
                            }

                            // Source
                            if (row[(int)TableMappingMetadataColumns.SourceTable] != DBNull.Value)
                            {
                                sourceTable = (string)row[(int)TableMappingMetadataColumns.SourceTable];
                            }

                            // Source Connection
                            if (row[(int)TableMappingMetadataColumns.SourceConnection] != DBNull.Value)
                            {
                                sourceConnectionKey = (string)row[TableMappingMetadataColumns.SourceConnection.ToString()];
                            }

                            // Target
                            if (row[(int)TableMappingMetadataColumns.TargetTable] != DBNull.Value)
                            {
                                targetTable = (string)row[(int)TableMappingMetadataColumns.TargetTable];
                            }

                            // Target Connection
                            if (row[(int)TableMappingMetadataColumns.TargetConnection] != DBNull.Value)
                            {
                                targetConnectionKey = (string)row[TableMappingMetadataColumns.TargetConnection.ToString()];
                            }

                            if (row[(int)TableMappingMetadataColumns.BusinessKeyDefinition] != DBNull.Value)
                            {
                                businessKeyDefinition = (string)row[(int)TableMappingMetadataColumns.BusinessKeyDefinition];
                                //businessKeyDefinition = businessKeyDefinition.Replace("'", "''");
                                //Double quotes for composites
                            }

                            if (row[(int)TableMappingMetadataColumns.DrivingKeyDefinition] != DBNull.Value)
                            {
                                drivingKeyDefinition = (string)row[(int)TableMappingMetadataColumns.DrivingKeyDefinition];
                                //drivingKeyDefinition = drivingKeyDefinition.Replace("'", "''");
                            }

                            if (row[(int)TableMappingMetadataColumns.FilterCriterion] != DBNull.Value)
                            {
                                filterCriterion = (string)row[(int)TableMappingMetadataColumns.FilterCriterion];
                                //filterCriterion = filterCriterion.Replace("'", "''");
                            }

                            try
                            {
                                var jsonTableMappingFull = new JArray();

                                // Load the file, if existing information needs to be merged
                                var mappingFileName = JsonHandling.FileConfiguration.tableMappingJsonFileName();
                                TableMappingJson[] jsonArray = JsonConvert.DeserializeObject<TableMappingJson[]>(File.ReadAllText(mappingFileName));

                                // Convert it into a JArray so segments can be added easily
                                if (jsonArray != null)
                                {
                                    jsonTableMappingFull = JArray.FromObject(jsonArray);
                                }

                                string[] inputHashValue = new string[] { versionId.ToString(), sourceTable, targetTable, businessKeyDefinition, drivingKeyDefinition, filterCriterion };
                                var hashKey = Utility.CreateMd5(inputHashValue, Utility.SandingElement);

                                // Convert it into a JArray so segments can be added easily
                                JObject newJsonSegment = new JObject(
                                    new JProperty("enabledIndicator", generateIndicator),
                                    new JProperty("tableMappingHash", hashKey),
                                    new JProperty("versionId", versionId),
                                    new JProperty("sourceTable", sourceTable),
                                    new JProperty("sourceConnectionKey", sourceConnectionKey),
                                    new JProperty("targetTable", targetTable),
                                    new JProperty("targetConnectionKey", targetConnectionKey),
                                    new JProperty("businessKeyDefinition", businessKeyDefinition),
                                    new JProperty("drivingKeyDefinition", drivingKeyDefinition),
                                    new JProperty("filterCriteria", filterCriterion)
                                );

                                jsonTableMappingFull.Add(newJsonSegment);

                                string output = JsonConvert.SerializeObject(jsonTableMappingFull, Formatting.Indented);
                                var outputFileName = JsonHandling.FileConfiguration.tableMappingJsonFileName();
                                File.WriteAllText(outputFileName, output);

                                //Making sure the hash key value is added to the data table as well
                                row[(int)TableMappingMetadataColumns.HashKey] = hashKey;

                            }
                            catch (JsonReaderException ex)
                            {
                                richTextBoxInformation.Text += "There were issues inserting the Json segment / record.\r\n" + ex;
                            }

                        }
                        #endregion

                        #region Deleted rows
                        //Deleted rows
                        if ((row.RowState & DataRowState.Deleted) != 0)
                        {
                            var hashKey = row[TableMappingMetadataColumns.HashKey.ToString(), DataRowVersion.Original].ToString();
                            var versionKey = row[TableMappingMetadataColumns.VersionId.ToString(), DataRowVersion.Original].ToString();

                            try
                            {
                                string inputFileName = JsonHandling.FileConfiguration.tableMappingJsonFileName();
                                var jsonArray = JsonConvert.DeserializeObject<TableMappingJson[]>(File.ReadAllText(inputFileName)).ToList();

                                //Retrieves the json segment in the file for the given hash returns value or NULL
                                var jsonSegment = jsonArray.FirstOrDefault(obj => obj.tableMappingHash == hashKey);

                                jsonArray.Remove(jsonSegment);

                                if (jsonSegment.tableMappingHash == "")
                                {
                                    richTextBoxInformation.Text += "The correct segment in the Json file was not found.\r\n";
                                }
                                else
                                {
                                    //Remove the segment from the JSON
                                    jsonArray.Remove(jsonSegment);
                                }

                                string output = JsonConvert.SerializeObject(jsonArray, Formatting.Indented);
                                string outputFileName = JsonHandling.FileConfiguration.tableMappingJsonFileName();
                                File.WriteAllText(outputFileName, output);

                            }
                            catch (JsonReaderException ex)
                            {
                                richTextBoxInformation.Text += "There were issues applying the Json update.\r\n" + ex;
                            }

                        }
                        #endregion
                    }

                    #region Statement execution
                    // Execute the statement. If the source is JSON this is done in separate calls for now

                    //Committing the changes to the data table - making sure new changes can be picked up
                    // AcceptChanges will clear all New, Deleted and/or Modified settings
                    dataTableChanges.AcceptChanges();
                    ((DataTable)_bindingSourceTableMetadata.DataSource).AcceptChanges();

                    //The JSON needs to be re-bound to the datatable / datagrid after being updated (accepted) to allow all values to be present including the hash which may have changed

                    //BindTableMappingJsonToDataTable();

                    #endregion

                    richTextBoxInformation.Text += "The Business Key / Table Mapping metadata has been saved.\r\n";
                }
            } // End of constructing the statements for insert / update / delete
        }

        private void SaveModelPhysicalModelMetadata(int versionId, DataTable dataTableChanges)
        {
            if (JsonHandling.FileConfiguration.newFilePhysicalModel == "true")
            {
                JsonHandling.RemoveExistingJsonFile(GlobalParameters.JsonModelMetadataFileName + @"_v" + GlobalParameters.CurrentVersionId + ".json");
                JsonHandling.CreatePlaceholderJsonFile(GlobalParameters.JsonModelMetadataFileName);
                JsonHandling.FileConfiguration.newFilePhysicalModel = "false";
            }

            //If the save version radiobutton is selected it means either minor or major version is checked and a full new snapshot needs to be created first
            if (!radiobuttonNoVersionChange.Checked)
            {

                    CreateNewPhysicalModelMetadataVersionJson(versionId);
                
            }

            //An in-place update (no change) to the existing version is done
            else
            {
                //Grabbing the generic settings from the main forms
                var insertQueryTables = new StringBuilder();

                if ((dataTableChanges != null && (dataTableChanges.Rows.Count > 0))) //Check if there are any changes made at all
                {
                    foreach (DataRow row in dataTableChanges.Rows) //Loop through the detected changes
                    {
                        #region Changed Rows
                        //Changed rows
                        if ((row.RowState & DataRowState.Modified) != 0)
                        {
                            var hashKey = (string) row["VERSION_ATTRIBUTE_HASH"];
                            var databaseName = (string) row["DATABASE_NAME"];
                            var schemaName = (string) row["SCHEMA_NAME"];
                            var tableName = (string) row["TABLE_NAME"];
                            var columnName = (string) row["COLUMN_NAME"];
                            var dataType = (string) row["DATA_TYPE"];
                            var maxLength = (string) row["CHARACTER_MAXIMUM_LENGTH"];
                            var numericPrecision = (string) row["NUMERIC_PRECISION"];
                            var ordinalPosition = (string) row["ORDINAL_POSITION"];
                            var primaryKeyIndicator = (string) row["PRIMARY_KEY_INDICATOR"];
                            var multiActiveIndicator = (string) row["MULTI_ACTIVE_INDICATOR"];
                            var versionKey = row["VERSION_ID"].ToString();


                            try
                            {
                                var inputFileName = JsonHandling.FileConfiguration.physicalModelJsonFileName();
                                PhysicalModelMetadataJson[] jsonArray =
                                    JsonConvert.DeserializeObject<PhysicalModelMetadataJson[]>(
                                        File.ReadAllText(inputFileName));

                                var jsonHash =
                                    jsonArray.FirstOrDefault(obj =>
                                        obj.versionAttributeHash ==
                                        hashKey); //Retrieves the json segment in the file for the given hash returns value or NULL

                                if (jsonHash.versionAttributeHash == "")
                                {
                                    richTextBoxInformation.Text +=
                                        "The correct segment in the Json file was not found.\r\n";
                                }
                                else
                                {
                                    // Update the values in the JSON segment
                                    jsonHash.databaseName = databaseName;
                                    jsonHash.schemaName = schemaName;
                                    jsonHash.tableName = tableName;
                                    jsonHash.columnName = columnName;
                                    jsonHash.dataType = dataType;
                                    jsonHash.characterMaximumLength = maxLength;
                                    jsonHash.numericPrecision = numericPrecision;
                                    jsonHash.ordinalPosition = ordinalPosition;
                                    jsonHash.primaryKeyIndicator = primaryKeyIndicator;
                                    jsonHash.multiActiveIndicator = multiActiveIndicator;
                                }

                                string output = JsonConvert.SerializeObject(jsonArray, Formatting.Indented);
                                string outputFileName = JsonHandling.FileConfiguration.physicalModelJsonFileName();
                                File.WriteAllText(outputFileName, output);
                            }
                            catch (JsonReaderException ex)
                            {
                                richTextBoxInformation.Text += "There were issues applying the JSON update.\r\n" + ex;
                            }

                        }

                        #endregion

                        // Insert new rows
                        if ((row.RowState & DataRowState.Added) != 0)
                        {
                            string databaseName = "";
                            string schemaName = "";
                            string tableName = "";
                            string columnName = "";
                            string dataType = "";
                            string maxLength = "0";
                            string numericPrecision = "0";
                            string ordinalPosition = "0";
                            string primaryKeyIndicator = "";
                            string multiActiveIndicator = "";


                            if (row[0] != DBNull.Value)
                            {
                                databaseName = (string)row[2];
                            }

                            if (row[1] != DBNull.Value)
                            {
                                schemaName = (string)row[3];
                            }

                            if (row[2] != DBNull.Value)
                            {
                                tableName = (string)row[4];
                            }

                            if (row[3] != DBNull.Value)
                            {
                                columnName = (string)row[5];
                            }

                            if (row[4] != DBNull.Value)
                            {
                                dataType = (string)row[6];
                            }

                            if (row[5] != DBNull.Value)
                            {
                                maxLength = (string)row[7];
                            }

                            if (row[6] != DBNull.Value)
                            {
                                numericPrecision = (string)row[8];
                            }

                            if (row[7] != DBNull.Value)
                            {
                                ordinalPosition = (string)row[9];
                            }

                            if (row[8] != DBNull.Value)
                            {
                                primaryKeyIndicator = (string)row[10];
                            }

                            if (row[9] != DBNull.Value)
                            {
                                multiActiveIndicator = (string)row[11];
                            }
                            try
                                {
                                    var jsonPhysicalModelMappingFull = new JArray();

                                    // Load the file, if existing information needs to be merged
                                    string inputFileName = JsonHandling.FileConfiguration.physicalModelJsonFileName();
                                    PhysicalModelMetadataJson[] jsonArray =
                                        JsonConvert.DeserializeObject<PhysicalModelMetadataJson[]>(
                                            File.ReadAllText(inputFileName));

                                    // Convert it into a JArray so segments can be added easily
                                    if (jsonArray != null)
                                    {
                                        jsonPhysicalModelMappingFull = JArray.FromObject(jsonArray);
                                    }
                                    //Generate a unique key using a hash

                                    string[] inputHashValue = new string[] { versionId.ToString(), tableName, columnName };
                                    var hashKey = Utility.CreateMd5(inputHashValue, Utility.SandingElement);

                                    JObject newJsonSegment = new JObject(
                                            new JProperty("versionAttributeHash", hashKey),
                                            new JProperty("versionId", versionId),
                                            new JProperty("databaseName", databaseName),
                                            new JProperty("schemaName", schemaName),
                                            new JProperty("tableName", tableName),
                                            new JProperty("columnName", columnName),
                                            new JProperty("dataType", dataType),
                                            new JProperty("characterMaximumLength", maxLength),
                                            new JProperty("numericPrecision", numericPrecision),
                                            new JProperty("ordinalPosition", ordinalPosition),
                                            new JProperty("primaryKeyIndicator", primaryKeyIndicator),
                                            new JProperty("multiActiveIndicator", multiActiveIndicator)
                                        );

                                    jsonPhysicalModelMappingFull.Add(newJsonSegment);

                                    string output = JsonConvert.SerializeObject(jsonPhysicalModelMappingFull, Formatting.Indented);
                                    string outputFileName = JsonHandling.FileConfiguration.physicalModelJsonFileName();
                                    File.WriteAllText(outputFileName, output);

                                    //Making sure the hash key value is added to the datatable as well
                                    row[(int)TableMappingMetadataColumns.HashKey] = hashKey;

                                }
                                catch (JsonReaderException ex)
                                {
                                    richTextBoxInformation.Text += "There were issues inserting the JSON segment / record.\r\n" + ex;
                                }

                        }

                        #region Deleted Rows
                        //Deleted rows
                        if ((row.RowState & DataRowState.Deleted) != 0)
                        {
                            var hashKey = row["VERSION_ATTRIBUTE_HASH", DataRowVersion.Original].ToString();
                            var versionKey = row["VERSION_ID", DataRowVersion.Original].ToString();

                                try
                                {
                                    string inputFileName = JsonHandling.FileConfiguration.physicalModelJsonFileName();
                                    var jsonArray = JsonConvert.DeserializeObject<PhysicalModelMetadataJson[]>(File.ReadAllText(inputFileName)).ToList();

                                    //Retrieves the json segment in the file for the given hash returns value or NULL
                                    var jsonSegment = jsonArray.FirstOrDefault(obj => obj.versionAttributeHash == hashKey);

                                    jsonArray.Remove(jsonSegment);

                                    if (jsonSegment.versionAttributeHash == "")
                                    {
                                        richTextBoxInformation.Text += "The correct segment in the JSON file was not found.\r\n";
                                    }
                                    else
                                    {
                                        //Remove the segment from the JSON
                                        jsonArray.Remove(jsonSegment);
                                    }

                                    string output = JsonConvert.SerializeObject(jsonArray, Formatting.Indented);
                                    string outputFileName = JsonHandling.FileConfiguration.physicalModelJsonFileName();
                                    File.WriteAllText(outputFileName, output);

                                }
                                catch (JsonReaderException ex)
                                {
                                    richTextBoxInformation.Text += "There were issues applying the JSON update.\r\n" + ex;
                                }

                        }
                        #endregion

                    } // All changes have been processed.

                    #region Statement execution


                    //Committing the changes to the datatable
                    dataTableChanges.AcceptChanges();
                    ((DataTable)_bindingSourcePhysicalModelMetadata.DataSource).AcceptChanges();

                    //The JSON needs to be re-bound to the datatable / datagrid after being updated to allow all values to be present

                        BindModelMetadataJsonToDataTable();
                    

                    richTextBoxInformation.Text += "The (physical) model metadata has been saved.\r\n";
                    #endregion
                }
            }
        }

        private void SaveAttributeMappingMetadata(int versionId, DataTable dataTableChanges)
        {
            if (JsonHandling.FileConfiguration.newFileAttributeMapping == "true")
            {
                JsonHandling.RemoveExistingJsonFile(GlobalParameters.JsonAttributeMappingFileName + @"_v" + GlobalParameters.CurrentVersionId + ".json");
                JsonHandling.CreatePlaceholderJsonFile(GlobalParameters.JsonAttributeMappingFileName);
                JsonHandling.FileConfiguration.newFileAttributeMapping = "false";
            }

            //If the save version radiobutton is selected it means either minor or major version is checked and a full new snapshot needs to be created first
            if (!radiobuttonNoVersionChange.Checked)
            {
                CreateNewAttributeMappingMetadataVersionJson(versionId);
            }

            #region In-version change
            else //An update (no change) to the existing version is done with regular inserts, updates and deletes
            {
                if (dataTableChanges != null && (dataTableChanges.Rows.Count > 0))
                //Check if there are any changes made at all
                {
                    // Loop through the changes captured in the data table
                    foreach (DataRow row in dataTableChanges.Rows)
                    {
                        #region Updates in Attribute Mapping
                        // Updates
                        if ((row.RowState & DataRowState.Modified) != 0)
                        {
                            //Grab the attributes into local variables
                            var hashKey = (string) row["ATTRIBUTE_MAPPING_HASH"];
                            var versionKey = row["VERSION_ID"].ToString();
                            var stagingTable = "";
                            var stagingColumn = "";
                            var integrationTable = "";
                            var integrationColumn = "";
                            var notes = "";

                            if (row["SOURCE_TABLE"] != DBNull.Value)
                            {
                                stagingTable = (string) row["SOURCE_TABLE"];
                            }

                            if (row["SOURCE_COLUMN"] != DBNull.Value)
                            {
                                stagingColumn = (string) row["SOURCE_COLUMN"];
                            }

                            if (row["TARGET_TABLE"] != DBNull.Value)
                            {
                                integrationTable = (string) row["TARGET_TABLE"];
                            }

                            if (row["TARGET_COLUMN"] != DBNull.Value)
                            {
                                integrationColumn = (string) row["TARGET_COLUMN"];
                            }

                            if (row["NOTES"] != DBNull.Value)
                            {
                                notes = (string) row["NOTES"];
                            }

                            try
                            {
                                var inputFileName = JsonHandling.FileConfiguration.attributeMappingJsonFileName();
                                AttributeMappingJson[] jsonArray = JsonConvert.DeserializeObject<AttributeMappingJson[]>(File.ReadAllText(inputFileName));

                                var jsonHash = jsonArray.FirstOrDefault(obj => obj.attributeMappingHash == hashKey);
                                //Retrieves the json segment in the file for the given hash returns value or NULL

                                if (jsonHash.attributeMappingHash == "")
                                {
                                    richTextBoxInformation.Text += "The correct segment in the Json file was not found.\r\n";
                                }
                                else
                                {
                                    // Update the values in the JSON segment
                                    jsonHash.sourceTable = stagingTable;
                                    jsonHash.sourceAttribute = stagingColumn;
                                    jsonHash.targetTable = integrationTable;
                                    jsonHash.targetAttribute = integrationColumn;
                                    jsonHash.notes = notes;
                                }

                                string output = JsonConvert.SerializeObject(jsonArray, Formatting.Indented);
                                string outputFileName = JsonHandling.FileConfiguration.attributeMappingJsonFileName();
                                File.WriteAllText(outputFileName, output);
                            }
                            catch (JsonReaderException ex)
                            {
                                richTextBoxInformation.Text += "There were issues applying the Json update.\r\n" + ex;
                            }
                        }

                        #endregion

                        #region Inserts in Attribute Mapping

                        // Inserts
                        if ((row.RowState & DataRowState.Added) != 0)
                        {
                            var stagingTable = "";
                            var stagingColumn = "";
                            var integrationTable = "";
                            var integrationColumn = "";
                            var notes = "";

                            if (row[2] != DBNull.Value)
                            {
                                stagingTable = (string)row[2];
                            }

                            if (row[3] != DBNull.Value)
                            {
                                stagingColumn = (string)row[3];
                            }

                            if (row[4] != DBNull.Value)
                            {
                                integrationTable = (string)row[4];
                            }

                            if (row[5] != DBNull.Value)
                            {
                                integrationColumn = (string)row[5];
                            }

                            if (row[6] != DBNull.Value)
                            {
                                notes = (string)row[6];
                            }


                                try
                                {
                                    var jsonAttributeMappingFull = new JArray();

                                    // Load the file, if existing information needs to be merged
                                    string inputFileName =
                                        JsonHandling.FileConfiguration.attributeMappingJsonFileName();
                                    AttributeMappingJson[] jsonArray =
                                        JsonConvert.DeserializeObject<AttributeMappingJson[]>(File.ReadAllText(inputFileName));

                                    // Convert it into a JArray so segments can be added easily
                                    if (jsonArray != null)
                                    {
                                        jsonAttributeMappingFull = JArray.FromObject(jsonArray);
                                    }

                                    string[] inputHashValue = new string[] { versionId.ToString(), stagingTable, stagingColumn, integrationTable, integrationColumn, notes };
                                    var hashKey = Utility.CreateMd5(inputHashValue, Utility.SandingElement);

                                    JObject newJsonSegment = new JObject(
                                        new JProperty("attributeMappingHash", hashKey),
                                        new JProperty("versionId", versionId),
                                        new JProperty("sourceTable", stagingTable),
                                        new JProperty("sourceAttribute", stagingColumn),
                                        new JProperty("targetTable", integrationTable),
                                        new JProperty("targetAttribute", integrationColumn),
                                        new JProperty("notes", notes)
                                        );

                                    jsonAttributeMappingFull.Add(newJsonSegment);

                                    string output = JsonConvert.SerializeObject(jsonAttributeMappingFull, Formatting.Indented);
                                    string outputFileName = JsonHandling.FileConfiguration.attributeMappingJsonFileName();
                                    File.WriteAllText(outputFileName, output);

                                    //Making sure the hash key value is added to the datatable as well
                                    row[(int)TableMappingMetadataColumns.HashKey] = hashKey;

                                }
                                catch (JsonReaderException ex)
                                {
                                    richTextBoxInformation.Text +=
                                        "There were issues inserting the JSON segment / record.\r\n" + ex;
                                }
                            

                        }

                        #endregion

                        #region Deletes in Attribute Mapping

                        // Deletes
                        if ((row.RowState & DataRowState.Deleted) != 0)
                        {
                            var hashKey = row["ATTRIBUTE_MAPPING_HASH", DataRowVersion.Original].ToString();
                            var versionKey = row["VERSION_ID", DataRowVersion.Original].ToString();

                                try
                                {
                                    string inputFileName =
                                        JsonHandling.FileConfiguration.attributeMappingJsonFileName();
                                    var jsonArray =
                                        JsonConvert.DeserializeObject<AttributeMappingJson[]>(File.ReadAllText(inputFileName)).ToList();

                                    //Retrieves the json segment in the file for the given hash returns value or NULL
                                    var jsonSegment =
                                        jsonArray.FirstOrDefault(obj => obj.attributeMappingHash == hashKey);

                                    jsonArray.Remove(jsonSegment);

                                    if (jsonSegment.attributeMappingHash == "")
                                    {
                                        richTextBoxInformation.Text +=
                                            "The correct segment in the JSON file was not found.\r\n";
                                    }
                                    else
                                    {
                                        //Remove the segment from the JSON
                                        jsonArray.Remove(jsonSegment);
                                    }

                                    string output = JsonConvert.SerializeObject(jsonArray, Formatting.Indented);
                                    string outputFileName = JsonHandling.FileConfiguration.attributeMappingJsonFileName();
                                    File.WriteAllText(outputFileName, output);

                                }
                                catch (JsonReaderException ex)
                                {
                                    richTextBoxInformation.Text += "There were issues applying the Json update.\r\n" + ex;
                                }

                        }

                        #endregion
                    }

                    #region Statement execution

                    //Committing the changes to the datatable
                    dataTableChanges.AcceptChanges();
                    ((DataTable)_bindingSourceAttributeMetadata.DataSource).AcceptChanges();

                    //The JSON needs to be re-bound to the datatable / datagrid after being updated to allow all values to be present
                        BindAttributeMappingJsonToDataTable();
                    

                    richTextBoxInformation.Text += "The Attribute Mapping metadata has been saved.\r\n";

                    #endregion
                }
            }
            #endregion

        }

        /// <summary>
        /// Load a Json Table Mapping file and bind it to the bindingsource.
        /// </summary>
        //private void BindTableMappingJsonToDataTable()
        //{
        //    var inputFileName = JsonHandling.FileConfiguration.tableMappingJsonFileName();

        //    Load the table mapping file, convert it to a DataTable and bind it to the source
        //    List<TableMappingJson> jsonArray = JsonConvert.DeserializeObject<List<TableMappingJson>>(File.ReadAllText(inputFileName));
        //    DataTable dt = Utility.ConvertToDataTable(jsonArray);

        //    Make sure the changes are seen as committed, so that changes can be detected later on.
        //    dt.AcceptChanges();

        //    SetTeamDataTableProperties.SetTableDataTableColumns(dt);
        //    _bindingSourceTableMetadata.DataSource = dt;
        //}

        private void BindAttributeMappingJsonToDataTable()
        {
            var inputFileName = JsonHandling.FileConfiguration.attributeMappingJsonFileName();

            // Load the attribute mapping file, convert it to a DataTable and bind it to the source
            List<AttributeMappingJson> jsonArray = JsonConvert.DeserializeObject<List<AttributeMappingJson>>(File.ReadAllText(inputFileName));
            DataTable dt = Utility.ConvertToDataTable(jsonArray);

            //Make sure the changes are seen as committed, so that changes can be detected later on.
            dt.AcceptChanges(); 

            SetTeamDataTableProperties.SetAttributeDataTableColumns(dt);
            _bindingSourceAttributeMetadata.DataSource = dt;
        }

        private void BindModelMetadataJsonToDataTable()
        {
            var inputFileName = JsonHandling.FileConfiguration.physicalModelJsonFileName();

            // Load the table mapping file, convert it to a DataTable and bind it to the source
            List<PhysicalModelMetadataJson> jsonArray = JsonConvert.DeserializeObject<List<PhysicalModelMetadataJson>>(File.ReadAllText(inputFileName));
            DataTable dt = Utility.ConvertToDataTable(jsonArray);

            //Make sure the changes are seen as committed, so that changes can be detected later on.
            dt.AcceptChanges(); 

            SetTeamDataTableProperties.SetPhysicalModelDataTableColumns(dt);
            _bindingSourcePhysicalModelMetadata.DataSource = dt;
        }

        /// <summary>
        ///   Load a Table Mapping Metadata Json or XML file into the datagrid
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void openMetadataFileToolStripMenuItem_Click_1(object sender, EventArgs e)
        {
            var theDialog = new OpenFileDialog
            {
                Title = @"Open Business Key Metadata File",
                Filter = @"Business Key files|*.xml;*.json",
                InitialDirectory = GlobalParameters.ConfigurationPath //Application.StartupPath + @"\Configuration\"
            };

            var ret = STAShowDialog(theDialog);

            if (ret == DialogResult.OK)
            {
                richTextBoxInformation.Clear();
                try
                {
                    var chosenFile = theDialog.FileName;
                    var dataSet = new DataSet();

                    string fileExtension = Path.GetExtension(theDialog.FileName);

                    if (fileExtension == ".xml" || fileExtension == ".XML")
                    {
                        dataSet.ReadXml(chosenFile);

                        dataGridViewTableMetadata.DataSource = dataSet.Tables[0];
                        _bindingSourceTableMetadata.DataSource = dataGridViewTableMetadata.DataSource;

                    }
                    else if (fileExtension == ".json" || fileExtension == ".JSON")
                    {
                        // Create a backup file, if enabled
                        if (checkBoxBackupFiles.Checked)
                        {
                            try
                            {
                                var backupFile = new JsonHandling();
                                var targetFileName = backupFile.BackupJsonFile(GlobalParameters.JsonTableMappingFileName + @"_v" + GlobalParameters.CurrentVersionId +".json", FormBase.GlobalParameters.ConfigurationPath);
                                richTextBoxInformation.Text ="A backup of the in-use JSON file was created as " + targetFileName + ".\r\n\r\n";
                            }
                            catch (Exception exception)
                            {
                                richTextBoxInformation.Text ="An issue occured when trying to make a backup of the in-use JSON file. The error message was " +exception + ".";
                            }
                        }

                        // If the information needs to be merged, a global parameter needs to be set.
                        // This will overwrite existing files for the in-use version.
                        if (!checkBoxMergeFiles.Checked)
                        {
                            JsonHandling.FileConfiguration.newFileTableMapping = "true";
                        }

                        // Load the file, convert it to a DataTable and bind it to the source
                        //List<TableMappingJson> jsonArray = JsonConvert.DeserializeObject<List<TableMappingJson>>(File.ReadAllText(chosenFile));
                        //DataTable dt = Utility.ConvertToDataTable(jsonArray);

                        TableMapping.GetTableMapping(chosenFile);

                        // Setup the datatable with proper headings.
                        TableMapping.SetTableDataTableColumns();

                        // Sort the columns
                        TableMapping.SetTableDataTableSorting();

                        // Clear out the existing data from the grid
                        _bindingSourceTableMetadata.DataSource = null;
                        _bindingSourceTableMetadata.Clear();
                        dataGridViewTableMetadata.DataSource = null;
 
                        // Bind the datatable to the gridview
                        _bindingSourceTableMetadata.DataSource = TableMapping.TableMappingDataTable;

                        //if (jsonArray != null)
                        //{
                            // Set the column header names
                            dataGridViewTableMetadata.DataSource = _bindingSourceTableMetadata;
                        //}
                    }

                    GridAutoLayoutTableMappingMetadata();
                    ContentCounter();
                    richTextBoxInformation.AppendText("The file " + chosenFile + " was loaded.\r\n");
                }
                catch (Exception ex)
                {
                    MessageBox.Show("An error has been encountered! The reported error is: "+ex);
                }
            }
        }



        private void CreateTemporaryWorkerTable(string connString)
        {
            var inputTableMapping = (DataTable)_bindingSourceTableMetadata.DataSource;
            var inputAttributeMapping = (DataTable)_bindingSourceAttributeMetadata.DataSource;
            var inputPhysicalModel = (DataTable)_bindingSourcePhysicalModelMetadata.DataSource;

            #region Attribute Mapping
            // Attribute mapping
            var createStatement = new StringBuilder();
            createStatement.AppendLine();
            createStatement.AppendLine("-- Attribute mapping");
            createStatement.AppendLine("IF OBJECT_ID('[TMP_MD_ATTRIBUTE_MAPPING]', 'U') IS NOT NULL");
            createStatement.AppendLine(" DROP TABLE [TMP_MD_ATTRIBUTE_MAPPING]");
            createStatement.AppendLine("");
            createStatement.AppendLine("CREATE TABLE [TMP_MD_ATTRIBUTE_MAPPING]");
            createStatement.AppendLine("( ");
            createStatement.AppendLine("    [ATTRIBUTE_MAPPING_HASH] AS(");
            createStatement.AppendLine("                CONVERT([CHAR](32),HASHBYTES('MD5',");
            createStatement.AppendLine("                ISNULL(RTRIM(CONVERT(VARCHAR(100),[TARGET_TABLE])),'NA')+'|'+");
            createStatement.AppendLine("                ISNULL(RTRIM(CONVERT(VARCHAR(100),[TARGET_COLUMN])),'NA')+'|'+");
            createStatement.AppendLine("                ISNULL(RTRIM(CONVERT(VARCHAR(100),[SOURCE_TABLE])),'NA')+'|'+");
            createStatement.AppendLine("                ISNULL(RTRIM(CONVERT(VARCHAR(100),[SOURCE_COLUMN])),'NA')+'|' +");
            createStatement.AppendLine("                ISNULL(RTRIM(CONVERT(VARCHAR(100),[NOTES])),'NA')+'|'");
            createStatement.AppendLine("			),(2)");
            createStatement.AppendLine("		)");
            createStatement.AppendLine("	) PERSISTED NOT NULL,");
            createStatement.AppendLine("	[VERSION_ID]          integer NOT NULL,");
            createStatement.AppendLine("	[SOURCE_TABLE]        varchar(100)  NULL,");
            createStatement.AppendLine("	[SOURCE_TABLE_TYPE]   varchar(100)  NULL,");
            createStatement.AppendLine("	[SOURCE_COLUMN]       varchar(100)  NULL,");
            createStatement.AppendLine("	[TARGET_TABLE]        varchar(100)  NULL,");
            createStatement.AppendLine("	[TARGET_TABLE_TYPE]   varchar(100)  NULL,");
            createStatement.AppendLine("	[TARGET_COLUMN]       varchar(100)  NULL,");
            createStatement.AppendLine("	[NOTES] varchar(4000)  NULL,");
            createStatement.AppendLine("   CONSTRAINT [PK_TMP_MD_ATTRIBUTE_MAPPING] PRIMARY KEY CLUSTERED ([ATTRIBUTE_MAPPING_HASH] ASC, [VERSION_ID] ASC)");
            createStatement.AppendLine(")");

            executeSqlCommand(createStatement, connString);
            createStatement.Clear();

            foreach (DataRow row in inputAttributeMapping.Rows)
            {
                //LBM 2019-01-31 -- ENSURE NO NULLS ARE INSERTED IN THE TABLE
                string sourceTable       = "";
                string SOURCE_COLUMN      ="";
                string targetTable       ="";
                string TARGET_COLUMN      ="";
                string NOTES="";

                if (row["SOURCE_TABLE"] != DBNull.Value)
                    sourceTable = (string)row["SOURCE_TABLE"];
                if (row["SOURCE_COLUMN"] != DBNull.Value)
                    SOURCE_COLUMN = (string)row["SOURCE_COLUMN"];
                if (row["TARGET_TABLE"] != DBNull.Value)
                    targetTable = (string)row["TARGET_TABLE"];
                if (row["TARGET_COLUMN"] != DBNull.Value)
                    TARGET_COLUMN = (string)row["TARGET_COLUMN"];
                if (row["NOTES"] != DBNull.Value)
                    NOTES = (string)row["NOTES"];

                var fullyQualifiedSourceName = MetadataHandling.GetFullyQualifiedTableName(sourceTable);
                var sourceType = MetadataHandling.GetTableType(sourceTable, "", TeamConfigurationSettings);

                var fullyQualifiedTargetName = MetadataHandling.GetFullyQualifiedTableName(targetTable);
                var targetType = MetadataHandling.GetTableType(targetTable, "", TeamConfigurationSettings);

                createStatement.AppendLine("INSERT[dbo].[TMP_MD_ATTRIBUTE_MAPPING] ([VERSION_ID], [SOURCE_TABLE], [SOURCE_TABLE_TYPE], [SOURCE_COLUMN], [TARGET_TABLE], [TARGET_TABLE_TYPE], [TARGET_COLUMN], [NOTES]) VALUES(0, N'" + fullyQualifiedSourceName + "', '"+sourceType+"' ,N'" + SOURCE_COLUMN + "', N'" + fullyQualifiedTargetName + "', '"+targetType+"' , N'" + TARGET_COLUMN + "', N'" + NOTES+ "');");
            }

            executeSqlCommand(createStatement, connString);
            createStatement.Clear();
            #endregion


            // Table Mapping
            createStatement.AppendLine();
            createStatement.AppendLine("-- Table Mapping");
            createStatement.AppendLine("IF OBJECT_ID('[TMP_MD_TABLE_MAPPING]', 'U') IS NOT NULL");
            createStatement.AppendLine(" DROP TABLE[TMP_MD_TABLE_MAPPING]");
            createStatement.AppendLine("");
            createStatement.AppendLine("CREATE TABLE[TMP_MD_TABLE_MAPPING]");
            createStatement.AppendLine("( ");
            createStatement.AppendLine("    [TABLE_MAPPING_HASH] AS(");
            createStatement.AppendLine("                CONVERT([CHAR](32),HASHBYTES('MD5',");
            createStatement.AppendLine("                ISNULL(RTRIM(CONVERT(VARCHAR(100),[TARGET_TABLE])),'NA')+'|'+");
            createStatement.AppendLine("                ISNULL(RTRIM(CONVERT(VARCHAR(100),[SOURCE_TABLE])),'NA')+'|'+");
            createStatement.AppendLine("                ISNULL(RTRIM(CONVERT(VARCHAR(100),[BUSINESS_KEY_ATTRIBUTE])),'NA')+'|'+");
            createStatement.AppendLine("                ISNULL(RTRIM(CONVERT(VARCHAR(100),[DRIVING_KEY_ATTRIBUTE])),'NA')+'|'+");
            createStatement.AppendLine("                ISNULL(RTRIM(CONVERT(VARCHAR(100),[FILTER_CRITERIA])),'NA')+'|'");
            createStatement.AppendLine("			),(2)");
            createStatement.AppendLine("			)");
            createStatement.AppendLine("		) PERSISTED NOT NULL ,");
            createStatement.AppendLine("	[VERSION_ID] integer NOT NULL ,");
            createStatement.AppendLine("	[SOURCE_TABLE] varchar(100)  NULL,");
            createStatement.AppendLine("	[SOURCE_TABLE_TYPE] varchar(100)  NULL,");
            createStatement.AppendLine("	[BUSINESS_KEY_ATTRIBUTE] varchar(4000)  NULL,");
            createStatement.AppendLine("	[DRIVING_KEY_ATTRIBUTE] varchar(4000)  NULL,");
            createStatement.AppendLine("	[TARGET_TABLE] varchar(100)  NULL,");
            createStatement.AppendLine("	[TARGET_TABLE_TYPE] varchar(100)  NULL,");
            createStatement.AppendLine("	[FILTER_CRITERIA] varchar(4000)  NULL,");
            createStatement.AppendLine("	[ENABLED_INDICATOR] varchar(5)  NULL,");
            createStatement.AppendLine("    CONSTRAINT [PK_TMP_MD_TABLE_MAPPING] PRIMARY KEY CLUSTERED([TABLE_MAPPING_HASH] ASC, [VERSION_ID] ASC)");
            createStatement.AppendLine(")");

            executeSqlCommand(createStatement, connString);
            createStatement.Clear();

            foreach (DataRow row in inputTableMapping.Rows)
            {
                string sourceTable = "";
                string BUSINESS_KEY_ATTRIBUTE = "";
                string targetTable = "";
                string FILTER_CRITERIA = "";
                string DRIVING_KEY_ATTRIBUTE = "";
                string ENABLED_INDICATOR = "";

                if (row[TableMappingMetadataColumns.SourceTable.ToString()] != DBNull.Value)
                    sourceTable = (string)row[TableMappingMetadataColumns.SourceTable.ToString()];
                if (row[TableMappingMetadataColumns.BusinessKeyDefinition.ToString()] != DBNull.Value) 
                    BUSINESS_KEY_ATTRIBUTE = (string)row[TableMappingMetadataColumns.BusinessKeyDefinition.ToString()];
                if (row[TableMappingMetadataColumns.TargetTable.ToString()] != DBNull.Value)
                    targetTable = (string)row[TableMappingMetadataColumns.TargetTable.ToString()];
                if (row[TableMappingMetadataColumns.FilterCriterion.ToString()] != DBNull.Value)
                {
                    FILTER_CRITERIA = (string)row[TableMappingMetadataColumns.FilterCriterion.ToString()];
                    FILTER_CRITERIA = FILTER_CRITERIA.Replace("'", "''");
                }
                if (row[TableMappingMetadataColumns.DrivingKeyDefinition.ToString()] != DBNull.Value)
                    DRIVING_KEY_ATTRIBUTE = (string)row[TableMappingMetadataColumns.DrivingKeyDefinition.ToString()];

                if (row[TableMappingMetadataColumns.Enabled.ToString()] != DBNull.Value)
                    ENABLED_INDICATOR = (string)row[TableMappingMetadataColumns.Enabled.ToString()].ToString();

                var fullyQualifiedSourceName = MetadataHandling.GetFullyQualifiedTableName(sourceTable);
                var sourceType = MetadataHandling.GetTableType(sourceTable,"", TeamConfigurationSettings);

                var fullyQualifiedTargetName = MetadataHandling.GetFullyQualifiedTableName(targetTable);
                var targetType = MetadataHandling.GetTableType(targetTable, "", TeamConfigurationSettings);

                createStatement.AppendLine("INSERT [dbo].[TMP_MD_TABLE_MAPPING] ([VERSION_ID], [SOURCE_TABLE], [SOURCE_TABLE_TYPE], [BUSINESS_KEY_ATTRIBUTE], [TARGET_TABLE], [TARGET_TABLE_TYPE], [FILTER_CRITERIA], [DRIVING_KEY_ATTRIBUTE], [ENABLED_INDICATOR]) VALUES(0, N'" + fullyQualifiedSourceName + "', '"+sourceType+"' , N'" + BUSINESS_KEY_ATTRIBUTE.Replace("'","''") + "', N'" + fullyQualifiedTargetName + "', '"+targetType+"' , N'" + FILTER_CRITERIA + "', '" + DRIVING_KEY_ATTRIBUTE + "', '" + ENABLED_INDICATOR + "');");
            }

            try
            {
                executeSqlCommand(createStatement, connString);
            }
            catch (Exception ex)
            {
                GlobalParameters.TeamEventLog.Add(Event.CreateNewEvent(EventTypes.Warning, $"A row could not be inserted into the temporary worker table TMP_MD_TABLE_MAPPING. The message is {ex} for the statement {createStatement}."));
            }

            createStatement.Clear();


            // Physical Model
            createStatement.AppendLine();
            createStatement.AppendLine("-- Version Attribute");
            createStatement.AppendLine("IF OBJECT_ID('[TMP_MD_VERSION_ATTRIBUTE]', 'U') IS NOT NULL");
            createStatement.AppendLine(" DROP TABLE[TMP_MD_VERSION_ATTRIBUTE]");
            createStatement.AppendLine("");
            createStatement.AppendLine("CREATE TABLE[TMP_MD_VERSION_ATTRIBUTE]");
            createStatement.AppendLine("( ");
            createStatement.AppendLine("");
            createStatement.AppendLine("    [VERSION_ATTRIBUTE_HASH] AS(");
            createStatement.AppendLine("                CONVERT([CHAR](32),HASHBYTES('MD5',");
            createStatement.AppendLine("                ISNULL(RTRIM(CONVERT(VARCHAR(100),[DATABASE_NAME])),'NA')+'|'+");
            createStatement.AppendLine("                ISNULL(RTRIM(CONVERT(VARCHAR(100),[SCHEMA_NAME])),'NA')+'|'+");
            createStatement.AppendLine("                ISNULL(RTRIM(CONVERT(VARCHAR(100),[TABLE_NAME])),'NA')+'|'+");
            createStatement.AppendLine("                ISNULL(RTRIM(CONVERT(VARCHAR(100),[COLUMN_NAME])),'NA')+'|'+");
            createStatement.AppendLine("                ISNULL(RTRIM(CONVERT(VARCHAR(100),[VERSION_ID])),'NA')+'|'");
            createStatement.AppendLine("			),(2)");
            createStatement.AppendLine("			)");
            createStatement.AppendLine("		) PERSISTED NOT NULL ,");
            createStatement.AppendLine("	[VERSION_ID] integer NOT NULL ,");
            createStatement.AppendLine("	[DATABASE_NAME]      varchar(100)  NOT NULL ,");
            createStatement.AppendLine("	[SCHEMA_NAME]        varchar(100)  NOT NULL ,");
            createStatement.AppendLine("	[TABLE_NAME]         varchar(100)  NOT NULL ,");
            createStatement.AppendLine("	[COLUMN_NAME]        varchar(100)  NOT NULL,");
            createStatement.AppendLine("    [DATA_TYPE]          varchar(100)  NOT NULL ,");
            createStatement.AppendLine("	[CHARACTER_MAXIMUM_LENGTH] integer NULL,");
            createStatement.AppendLine("    [NUMERIC_PRECISION]  integer NULL,");
            createStatement.AppendLine("    [ORDINAL_POSITION]   integer NULL,");
            createStatement.AppendLine("    [PRIMARY_KEY_INDICATOR] varchar(1)  NULL ,");
            createStatement.AppendLine("	[MULTI_ACTIVE_INDICATOR] varchar(1)  NULL ");
            createStatement.AppendLine(")");
            createStatement.AppendLine("");
            createStatement.AppendLine("ALTER TABLE [TMP_MD_VERSION_ATTRIBUTE]");
            createStatement.AppendLine("    ADD CONSTRAINT[PK_TMP_MD_VERSION_ATTRIBUTE] PRIMARY KEY CLUSTERED([DATABASE_NAME] ASC, [SCHEMA_NAME], [TABLE_NAME], [COLUMN_NAME], [VERSION_ID] ASC)");
            createStatement.AppendLine();

            executeSqlCommand(createStatement, connString);
            createStatement.Clear();

            // Load the datatable into the worker table for the physical model 
            foreach (DataRow row in inputPhysicalModel.Rows)
            {
                string databaseName = "";
                string schemaName = "";
                string tableName = "";
                string columnName = "";

                if (row["DATABASE_NAME"] != DBNull.Value)
                    databaseName = (string)row["DATABASE_NAME"];
                if (row["SCHEMA_NAME"] != DBNull.Value)
                    schemaName = (string)row["SCHEMA_NAME"];
                if (row["TABLE_NAME"] != DBNull.Value) 
                    tableName = (string)row["TABLE_NAME"];
                if (row["COLUMN_NAME"] != DBNull.Value)
                    columnName = (string)row["COLUMN_NAME"];

                createStatement.AppendLine("INSERT [dbo].[TMP_MD_VERSION_ATTRIBUTE]" +
                                           " ([VERSION_ID], " +
                                           "[DATABASE_NAME], " +
                                           "[SCHEMA_NAME], " +
                                           "[TABLE_NAME], " +
                                           "[COLUMN_NAME], " +
                                           "[DATA_TYPE], " +
                                           "[CHARACTER_MAXIMUM_LENGTH], " +
                                           "[NUMERIC_PRECISION], " +
                                           "[ORDINAL_POSITION], " +
                                           "[PRIMARY_KEY_INDICATOR], " +
                                           "[MULTI_ACTIVE_INDICATOR]) " +
                                           "VALUES(" +
                                           "0, " +
                                           "N'" + databaseName + "', " +
                                           "N'" + schemaName + "', " +
                                           "N'" + tableName + "', " +
                                           "N'" + columnName + "', " +
                                           "N'" + (string)row["DATA_TYPE"] + "', " +
                                           "N'" + (string)row["CHARACTER_MAXIMUM_LENGTH"] + "', " +
                                           "N'" + (string)row["NUMERIC_PRECISION"] + "', " +
                                           "N'" + (string)row["ORDINAL_POSITION"] + "', " +
                                           "N'" + (string)row["PRIMARY_KEY_INDICATOR"] + "', " +
                                           "N'" + (string)row["MULTI_ACTIVE_INDICATOR"] + "');");
            }

            executeSqlCommand(createStatement, connString);
            createStatement.Clear();

        }

        private void executeSqlCommand(StringBuilder inputString, string connString)
        {
            using (var connectionVersion = new SqlConnection(connString))
            {
                var commandVersion = new SqlCommand(inputString.ToString(), connectionVersion);

                try
                {
                    connectionVersion.Open();
                    commandVersion.ExecuteNonQuery();

                }
                catch (Exception)
                {
                    // IGNORE FOR NOW
                }
            }
        }

        private void droptemporaryWorkerTable(string connString)
        {
            // Attribute mapping
            var createStatement = new StringBuilder();
            createStatement.AppendLine("-- Attribute mapping");
            createStatement.AppendLine("IF OBJECT_ID('[TMP_MD_ATTRIBUTE_MAPPING]', 'U') IS NOT NULL");
            createStatement.AppendLine(" DROP TABLE [TMP_MD_ATTRIBUTE_MAPPING]");

            executeSqlCommand(createStatement, connString);
            createStatement.Clear();

            // Table Mapping
            createStatement.AppendLine("-- Table Mapping");
            createStatement.AppendLine("IF OBJECT_ID('[TMP_MD_TABLE_MAPPING]', 'U') IS NOT NULL");
            createStatement.AppendLine(" DROP TABLE[TMP_MD_TABLE_MAPPING]");

            executeSqlCommand(createStatement, connString);
            createStatement.Clear();        
  
            // Physical Model
            createStatement.AppendLine("-- Version Attribute");
            createStatement.AppendLine("IF OBJECT_ID('[TMP_MD_VERSION_ATTRIBUTE]', 'U') IS NOT NULL");
            createStatement.AppendLine(" DROP TABLE [TMP_MD_VERSION_ATTRIBUTE]");

            executeSqlCommand(createStatement, connString);
            createStatement.Clear();
        }

        # region Background worker
        private void buttonStart_Click(object sender, EventArgs e)
        {
            #region Validation
            // The first thing to happen is to check if the validation needs to be run (and started if the answer to this is yes)
            if (checkBoxValidation.Checked)
            {
                if (radioButtonPhysicalMode.Checked == false && _bindingSourcePhysicalModelMetadata.Count == 0)
                {
                    richTextBoxInformation.Text += "There is no model metadata available, so the metadata can only be validated with the 'Ignore Version' enabled.\r\n ";
                }
                else
                {
                    if (backgroundWorkerValidationOnly.IsBusy) return;
                    // create a new instance of the alert form
                    _alertValidation = new Form_Alert();
                    _alertValidation.SetFormName("Validating the metadata");
                    // event handler for the Cancel button in AlertForm
                    _alertValidation.Canceled += buttonCancel_Click;
                    _alertValidation.Show();
                    // Start the asynchronous operation.
                    backgroundWorkerValidationOnly.RunWorkerAsync();
                    while (backgroundWorkerValidationOnly.IsBusy)
                    {
                        Application.DoEvents();
                    }
                }
            }
            #endregion

            // After validation finishes, the activation thread / process should start.
            // Only if the validation is enabled AND there are no issues identified in earlier validation checks.
            #region Activation
            if (!checkBoxValidation.Checked || (checkBoxValidation.Checked && MetadataParameters.ValidationIssues == 0))
            {
                // Commence the activation
                var conn = new SqlConnection { ConnectionString = TeamConfigurationSettings.MetadataConnection.CreateSqlServerConnectionString(false) };

                richTextBoxInformation.Clear();

               // var versionMajorMinor = GetVersion(trackBarVersioning.Value, conn);
                var versionMajorMinor = EnvironmentVersion.GetMajorMinorForVersionId(GlobalParameters.WorkingEnvironment, trackBarVersioning.Value);
                
                var majorVersion = versionMajorMinor.Item2;
                var minorVersion = versionMajorMinor.Item3;
                richTextBoxInformation.Text += "Commencing preparation / activation for version " + majorVersion + "." + minorVersion + ".\r\n";

                // Move data from the grids into temp tables
                CreateTemporaryWorkerTable(TeamConfigurationSettings.MetadataConnection.CreateSqlServerConnectionString(false));

                if (radioButtonPhysicalMode.Checked == false)
                {
                    var versionExistenceCheck = new StringBuilder();

                    versionExistenceCheck.AppendLine("SELECT * FROM TMP_MD_VERSION_ATTRIBUTE WHERE VERSION_ID = " + trackBarVersioning.Value);

                    var versionExistenceCheckDataTable = Utility.GetDataTable(ref conn, versionExistenceCheck.ToString());

                    if (versionExistenceCheckDataTable != null && versionExistenceCheckDataTable.Rows.Count > 0)
                    {
                        if (backgroundWorkerMetadata.IsBusy) return;
                        // create a new instance of the alert form
                        _alert = new Form_Alert();
                        // event handler for the Cancel button in AlertForm
                        _alert.Canceled += buttonCancel_Click;
                        _alert.Show();
                        // Start the asynchronous operation.
                        backgroundWorkerMetadata.RunWorkerAsync();
                    }
                    else
                    {
                        richTextBoxInformation.Text += "There is no model metadata available for this version, so the metadata can only be activated with the 'Ignore Version' enabled for this specific version.\r\n ";
                    }
                }
                else
                {
                    if (backgroundWorkerMetadata.IsBusy) return;
                    // create a new instance of the alert form
                    _alert = new Form_Alert();
                    // event handler for the Cancel button in AlertForm
                    _alert.Canceled += buttonCancel_Click;
                    _alert.Show();
                    // Start the asynchronous operation.
                    backgroundWorkerMetadata.RunWorkerAsync();
                }
            }
            else
            {
                richTextBoxInformation.Text = "Validation found issues which should be investigated. If you would like to continue, please uncheck the validation and activate the metadata again.\r\n ";
            }
            #endregion
        }

        /// <summary>
        /// This event handler cancels the background worker, fired from Cancel button in AlertForm.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>        
        private void buttonCancel_Click(object sender, EventArgs e)
        {
            if (backgroundWorkerMetadata.WorkerSupportsCancellation)
            {
                // Cancel the asynchronous operation.
                backgroundWorkerMetadata.CancelAsync();
                // Close the AlertForm
                _alertValidation.Close();
            }
        }

        /// <summary>
        /// Multi-threading for informing the user when version changes (to other forms).
        /// </summary>
        /// <returns></returns>
        delegate int GetVersionFromTrackBarCallBack();
        private int GetVersionFromTrackBar()
        {
            if (trackBarVersioning.InvokeRequired)
            {
                var d = new GetVersionFromTrackBarCallBack(GetVersionFromTrackBar);
                return Int32.Parse(Invoke(d).ToString());              
            }
            else
            {
               return trackBarVersioning.Value;
            }
        }

        // This event handler deals with the results of the background operation.
        private void backgroundWorkerMetadata_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (e.Cancelled)
            {
                labelResult.Text = "Cancelled!";
            }
            else if (e.Error != null)
            {
                labelResult.Text = "Error: " + e.Error.Message;
            }
            else
            {
                labelResult.Text = "Done!";
                richTextBoxInformation.Text += "The metadata was processed successfully!\r\n";

                #region Save the JSON interface files
                // Saving the interfaces to Json
                if (checkBoxSaveInterfaceToJson.Checked)
                {
                    // Take all the rows from the grid
                    List<DataGridViewRow> rowList = new List<DataGridViewRow>();
                    foreach (DataGridViewRow row in dataGridViewTableMetadata.Rows)
                    {
                        if (!row.IsNewRow)
                        {
                            rowList.Add(row); //add the row to the list
                        }
                    }

                    GenerateFromPattern(rowList);
                }
                #endregion
            }
            // Close the AlertForm
            //alert.Close();
        }

        // This event handler updates the progress.
        private void backgroundWorkerMetadata_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            // Show the progress in main form (GUI)
            labelResult.Text = (e.ProgressPercentage + "%");

            // Pass the progress to AlertForm label and progressbar
            _alert.Message = "In progress, please wait... " + e.ProgressPercentage + "%";
            _alert.ProgressValue = e.ProgressPercentage;
        }
        # endregion

        // This event handler is where the time-consuming work is done.
        private void backgroundWorkerMetadata_DoWorkMetadataActivation(object sender, DoWorkEventArgs e)
        {
            #region Generic
            // Set the stopwatch to be able to report back on process duration.
            Stopwatch totalProcess = new Stopwatch();
            Stopwatch subProcess = new Stopwatch();
            totalProcess.Start();

            BackgroundWorker worker = sender as BackgroundWorker;

            var inputTableMetadata = (DataTable)_bindingSourceTableMetadata.DataSource;
            var inputAttributeMetadata = (DataTable)_bindingSourceAttributeMetadata.DataSource;

            DataRow[] selectionRows;

            var errorLog = new StringBuilder();
            var errorCounter = new int();

            var connOmd = new SqlConnection { ConnectionString = TeamConfigurationSettings.MetadataConnection.CreateSqlServerConnectionString(false) };
            var connStg= new SqlConnection { ConnectionString = TeamConfigurationSettings.MetadataConnection.CreateSqlServerConnectionString(false) };
            var connPsa = new SqlConnection { ConnectionString = TeamConfigurationSettings.MetadataConnection.CreateSqlServerConnectionString(false) };
            var connInt = new SqlConnection { ConnectionString = TeamConfigurationSettings.MetadataConnection.CreateSqlServerConnectionString(false) };
            var connPres = new SqlConnection { ConnectionString = TeamConfigurationSettings.MetadataConnection.CreateSqlServerConnectionString(false) };

            var metaDataConnection = TeamConfigurationSettings.MetadataConnection.CreateSqlServerConnectionString(false);

            //// Get everything as local variables to reduce multi-threading issues
            //var integrationDatabase = '['+ TeamConfigurationSettings.IntegrationDatabaseName + ']';

            //var linkedServer = TeamConfigurationSettings.PhysicalModelServerName;
            //var metadataServer = TeamConfigurationSettings.MetadataConnection.databaseServer.databaseName;
            //if (linkedServer != "" && linkedServer != metadataServer)
            //{
            //    linkedServer = '[' + linkedServer + "].";
            //}
            //else
            //    linkedServer = "";

            var effectiveDateTimeAttribute = TeamConfigurationSettings.EnableAlternativeSatelliteLoadDateTimeAttribute=="True" ? TeamConfigurationSettings.AlternativeSatelliteLoadDateTimeAttribute : TeamConfigurationSettings.LoadDateTimeAttribute;
            var currentRecordAttribute = TeamConfigurationSettings.CurrentRowAttribute;
            var eventDateTimeAtttribute = TeamConfigurationSettings.EventDateTimeAttribute;
            var recordSource = TeamConfigurationSettings.RecordSourceAttribute;
            var alternativeRecordSource = TeamConfigurationSettings.AlternativeRecordSourceAttribute;
            var sourceRowId = TeamConfigurationSettings.RowIdAttribute;
            var recordChecksum = TeamConfigurationSettings.RecordChecksumAttribute;
            var changeDataCaptureIndicator = TeamConfigurationSettings.ChangeDataCaptureAttribute;
            var hubAlternativeLdts = TeamConfigurationSettings.AlternativeLoadDateTimeAttribute;
            var etlProcessId = TeamConfigurationSettings.EtlProcessAttribute;
            var loadDateTimeStamp = TeamConfigurationSettings.LoadDateTimeAttribute;

            var stagingPrefix = TeamConfigurationSettings.StgTablePrefixValue;
            var psaPrefix = TeamConfigurationSettings.PsaTablePrefixValue;
            var hubTablePrefix = TeamConfigurationSettings.HubTablePrefixValue;
            var lnkTablePrefix = TeamConfigurationSettings.LinkTablePrefixValue;
            var satTablePrefix = TeamConfigurationSettings.SatTablePrefixValue;
            var lsatTablePrefix = TeamConfigurationSettings.LsatTablePrefixValue;

            if (TeamConfigurationSettings.TableNamingLocation=="Prefix")
            {
                stagingPrefix = stagingPrefix + "%";
                psaPrefix = psaPrefix + "%";
                hubTablePrefix = hubTablePrefix + "%";
                lnkTablePrefix = lnkTablePrefix + "%";
                satTablePrefix = satTablePrefix + "%";
                lsatTablePrefix = lsatTablePrefix + "%";
            }
            else
            {
                stagingPrefix = "%" + stagingPrefix;
                psaPrefix = "%" + psaPrefix;
                hubTablePrefix = "%" + hubTablePrefix;
                lnkTablePrefix = "%" + lnkTablePrefix;
                satTablePrefix = "%" + satTablePrefix;
                lsatTablePrefix = "%" + lsatTablePrefix;
            }

            var dwhKeyIdentifier = TeamConfigurationSettings.DwhKeyIdentifier;

            if (TeamConfigurationSettings.KeyNamingLocation=="Prefix")
            {
                dwhKeyIdentifier = dwhKeyIdentifier + '%';
            }
            else
            {
                dwhKeyIdentifier = '%' + dwhKeyIdentifier;
            }

            // Handling multi-threading
            if (worker != null && worker.CancellationPending)
            {
                e.Cancel = true;
            }
            else
            {
                // Determine the version.
                var versionId = GetVersionFromTrackBar();

                var versionMajorMinor = EnvironmentVersion.GetMajorMinorForVersionId(GlobalParameters.WorkingEnvironment,versionId);
                var majorVersion = versionMajorMinor.Item2;
                var minorVersion = versionMajorMinor.Item3;

                // Determine the query type (physical or virtual).
                var queryMode = radioButtonPhysicalMode.Checked ? "physical" : "virtual";

                // Get the full dictionary of objects and connections.
                var localTableMappingConnectionDictionary = GetTableMappingConnections();

                // Get the dictionary of target data objects and their enabled / disabled flag.
                var localTableEnabledDictionary = GetEnabledForDataObject();

                // Event reporting - informing the user that the activation process has started.
                string eventMessage = "";
                eventMessage = "Commencing metadata preparation / activation for version " + majorVersion + "." + minorVersion + ".";
                GlobalParameters.TeamEventLog.Add(Event.CreateNewEvent(EventTypes.Information, eventMessage)); _alert.SetTextLogging(eventMessage);

                // Event reporting - alerting the user what kind of metadata is prepared.
                eventMessage = queryMode == "physical" ? "Physical Mode has been selected as metadata source for activation. This means that the database will be used to query physical model (table and attribute) metadata. In other words, the physical model versioning is ignored." : "Virtual Mode has been selected. This means that the versioned physical model in the data grid will be used as table and attribute metadata.";
                GlobalParameters.TeamEventLog.Add(Event.CreateNewEvent(EventTypes.Information, eventMessage));
                _alert.SetTextLogging("\r\n\r\n"+eventMessage);
                #endregion

                #region Delete Metadata - 2%

                // 1. Deleting metadata
                _alert.SetTextLogging("Commencing removal of existing metadata.\r\n");

                var deleteStatement = new StringBuilder();
                deleteStatement.AppendLine(@"
                                        DELETE FROM dbo.[MD_SOURCE_STAGING_XREF];
                                        DELETE FROM dbo.[MD_SOURCE_STAGING_ATTRIBUTE_XREF];
                                        DELETE FROM dbo.[MD_SOURCE_PERSISTENT_STAGING_XREF];
                                        DELETE FROM dbo.[MD_SOURCE_PERSISTENT_STAGING_ATTRIBUTE_XREF];
                                        DELETE FROM dbo.[MD_STAGING];
                                        DELETE FROM dbo.[MD_PERSISTENT_STAGING];
                                        DELETE FROM dbo.[MD_SOURCE_LINK_ATTRIBUTE_XREF];
                                        DELETE FROM dbo.[MD_SOURCE_SATELLITE_ATTRIBUTE_XREF];
                                        DELETE FROM dbo.[MD_SOURCE_LINK_XREF];
                                        DELETE FROM dbo.[MD_SOURCE_SATELLITE_XREF];
                                        DELETE FROM dbo.[MD_DRIVING_KEY_XREF];
                                        DELETE FROM dbo.[MD_HUB_LINK_XREF];
                                        DELETE FROM dbo.[MD_SATELLITE];
                                        DELETE FROM dbo.[MD_BUSINESS_KEY_COMPONENT_PART];
                                        DELETE FROM dbo.[MD_BUSINESS_KEY_COMPONENT];
                                        DELETE FROM dbo.[MD_SOURCE_HUB_XREF];
                                        DELETE FROM dbo.[MD_ATTRIBUTE];
                                        DELETE FROM dbo.[MD_SOURCE];
                                        DELETE FROM dbo.[MD_HUB];
                                        DELETE FROM dbo.[MD_LINK];
                                        DELETE FROM dbo.[MD_MODEL_METADATA];
                                        DELETE FROM dbo.[MD_PHYSICAL_MODEL];
                                        ");

                using (var connectionVersion = new SqlConnection(metaDataConnection))
                {
                    var commandVersion = new SqlCommand(deleteStatement.ToString(), connectionVersion);

                    try
                    {
                        connectionVersion.Open();
                        commandVersion.ExecuteNonQuery();

                        if (worker != null) worker.ReportProgress(2);
                        _alert.SetTextLogging("Removal of existing metadata completed.\r\n");
                    }
                    catch (Exception ex)
                    {
                        errorCounter++;
                        _alert.SetTextLogging(
                            "An issue has occured during removal of old metadata. Please check the Error Log for more details.\r\n");
                        errorLog.AppendLine("\r\nAn issue has occured during removal of old metadata: \r\n\r\n" + ex);
                        errorLog.AppendLine("\r\nThe query that caused the issue is: \r\n\r\n" + deleteStatement);
                    }
                }

                # endregion


                # region Prepare Version Information - 3%

                // 2. Prepare Version
                _alert.SetTextLogging("\r\n");
                _alert.SetTextLogging("Commencing preparing the version metadata.\r\n");

                var versionName = string.Concat(majorVersion, '.', minorVersion);

                using (var connection = new SqlConnection(metaDataConnection))
                {
                    _alert.SetTextLogging("-->  Working on committing version " + versionName +
                                          " to the metadata repository.\r\n");

                    var insertVersionStatement = new StringBuilder();
                    insertVersionStatement.AppendLine("INSERT INTO [MD_MODEL_METADATA]");
                    insertVersionStatement.AppendLine("([VERSION_NAME],[ACTIVATION_DATETIME])");
                    insertVersionStatement.AppendLine("VALUES ('" + versionName + "','" +
                                                      DateTime.Now.ToString("yyyy-MM-ddTHH:mm:sszzz") + "')");

                    var command = new SqlCommand(insertVersionStatement.ToString(), connection);

                    try
                    {
                        connection.Open();
                        command.ExecuteNonQuery();
                    }
                    catch (Exception ex)
                    {
                        errorCounter++;
                        _alert.SetTextLogging(
                            "An issue has occured during preparation of the version information. Please check the Error Log for more details.\r\n");
                        errorLog.AppendLine(
                            "\r\nAn issue has occured during preparation of the version information: \r\n\r\n" + ex);
                    }
                }

                if (worker != null) worker.ReportProgress(3);
                _alert.SetTextLogging("Preparation of the version details completed.\r\n");


                #endregion


                # region Prepare Source - 5%

                // Prepare the generic sources
                subProcess.Reset();
                subProcess.Start();

                _alert.SetTextLogging("\r\n");
                _alert.SetTextLogging("Commencing preparing the source metadata.\r\n");

                // Getting the distinct list of tables to go into the 'source'
                selectionRows = inputTableMetadata.Select(TableMappingMetadataColumns.Enabled.ToString()+" = 'true'");

                var distinctListSource = new List<string>
                {
                    // Create a dummy row
                    "Not applicable"
                };

                // Create a distinct list of sources from the datagrid
                foreach (DataRow row in selectionRows)
                {
                    string source_table = row[TableMappingMetadataColumns.SourceTable.ToString()].ToString().Trim();
                    if (!distinctListSource.Contains(source_table))
                    {
                        distinctListSource.Add(source_table);
                    }
                }

                // Add the list of sources to the MD_SOURCE table
                foreach (var tableName in distinctListSource)
                {
                    using (var connection = new SqlConnection(metaDataConnection))
                    {
                        if (tableName != "Not applicable")
                        {
                            _alert.SetTextLogging("--> " + tableName + "\r\n");
                        }

                        var fullyQualifiedName = MetadataHandling.GetSchema(tableName).FirstOrDefault();

                        var insertStatement = new StringBuilder();
                        insertStatement.AppendLine("INSERT INTO [MD_SOURCE]");
                        insertStatement.AppendLine("([SOURCE_NAME], [SOURCE_NAME_SHORT], [SCHEMA_NAME])");
                        insertStatement.AppendLine("VALUES ('" + tableName + "','" + fullyQualifiedName.Value + "','" + fullyQualifiedName.Key + "')");

                        var command = new SqlCommand(insertStatement.ToString(), connection);

                        try
                        {
                            connection.Open();
                            command.ExecuteNonQuery();
                        }
                        catch (Exception ex)
                        {
                            errorCounter++;
                            _alert.SetTextLogging(
                                "An issue has occured during preparation of the source metadata. Please check the Error Log for more details.\r\n");
                            errorLog.AppendLine(
                                "\r\nAn issue has occured during preparation of the source metadata: \r\n\r\n" + ex);
                            errorLog.AppendLine("\r\nThe query that caused the issue is: \r\n\r\n" + insertStatement);
                        }
                    }
                }

                worker?.ReportProgress(5);
                subProcess.Stop();
                _alert.SetTextLogging("Preparation of the source metadata completed, and has taken " + subProcess.Elapsed.TotalSeconds + " seconds.\r\n");

                #endregion


                #region Prepare Staging Area - 7%

                //Prepare the Staging Area
                subProcess.Reset();
                subProcess.Start();

                _alert.SetTextLogging("\r\n");
                _alert.SetTextLogging("Commencing preparing the Staging Area metadata.\r\n");

                // Getting the distinct list of tables to go into the MD_STAGING table
                if (TeamConfigurationSettings.TableNamingLocation == "Prefix")
                {
                    selectionRows = inputTableMetadata.Select(TableMappingMetadataColumns.Enabled.ToString() + " = 'true' AND "+TableMappingMetadataColumns.TargetTable.ToString()+" LIKE '" + TeamConfigurationSettings.StgTablePrefixValue + "%'");
                }
                else
                {
                    selectionRows = inputTableMetadata.Select(TableMappingMetadataColumns.Enabled.ToString() + " = 'true' AND " + TableMappingMetadataColumns.TargetTable.ToString() + " LIKE '%" + TeamConfigurationSettings.StgTablePrefixValue + "'");
                }

                var distinctListStg = new List<string>
                {
                    // Create a dummy row
                    "Not applicable"
                };

                // Create a distinct list of sources from the datagrid
                foreach (DataRow row in selectionRows)
                {
                    string target_table = row[TableMappingMetadataColumns.TargetTable.ToString()].ToString().Trim();
                    if (!distinctListStg.Contains(target_table))
                    {
                        distinctListStg.Add(target_table);
                    }
                }

                // Process the unique Staging Area records
                foreach (var tableName in distinctListStg)
                {
                    using (var connection = new SqlConnection(metaDataConnection))
                    {
                        if (tableName != "Not applicable")
                        {
                            _alert.SetTextLogging("--> " + tableName + "\r\n");
                        }

                        var fullyQualifiedName = MetadataHandling.GetSchema(tableName).FirstOrDefault();

                        var insertStatement = new StringBuilder();
                        insertStatement.AppendLine("INSERT INTO [MD_STAGING]");
                        insertStatement.AppendLine("([STAGING_NAME], [SCHEMA_NAME])");
                        insertStatement.AppendLine("VALUES ('" + fullyQualifiedName.Value + "','" + fullyQualifiedName.Key + "')");

                        var command = new SqlCommand(insertStatement.ToString(), connection);

                        try
                        {
                            connection.Open();
                            command.ExecuteNonQuery();
                        }
                        catch (Exception ex)
                        {
                            errorCounter++;
                            _alert.SetTextLogging(
                                "An issue has occured during preparation of the Staging Area. Please check the Error Log for more details.\r\n");

                            errorLog.AppendLine(
                                "\r\nAn issue has occured during preparation of the Staging Area: \r\n\r\n" + ex);
                            errorLog.AppendLine("\r\nThe query that caused the issue is: \r\n\r\n" + insertStatement);
                        }
                    }
                }

                worker?.ReportProgress(7);
                subProcess.Stop();
                _alert.SetTextLogging("Preparation of the Staging Area metadata completed, and has taken " +
                                      subProcess.Elapsed.TotalSeconds + " seconds.\r\n");

                #endregion


                #region Prepare Source to Staging Area XREF - 10%

                // Prepare the Source to Staging Area XREF
                subProcess.Reset();
                subProcess.Start();

                _alert.SetTextLogging("\r\n");
                _alert.SetTextLogging("Commencing preparing the relationship between Source and Staging Area.\r\n");

                // Getting the mapping list from the data table
                if (TeamConfigurationSettings.TableNamingLocation == "Prefix")
                {
                    selectionRows = inputTableMetadata.Select(TableMappingMetadataColumns.Enabled.ToString() + " = 'true' AND " + TableMappingMetadataColumns.TargetTable.ToString() + " LIKE '" + TeamConfigurationSettings.StgTablePrefixValue + "%'");
                }
                else
                {
                    selectionRows = inputTableMetadata.Select(TableMappingMetadataColumns.Enabled.ToString() + " = 'true' AND " + TableMappingMetadataColumns.TargetTable.ToString() + " LIKE '%" + TeamConfigurationSettings.StgTablePrefixValue + "'");
                }

                // Process the unique Staging Area records
                foreach (var row in selectionRows)
                {
                    using (var connection = new SqlConnection(metaDataConnection))
                    {
                        var sourceFullyQualifiedName = MetadataHandling.GetSchema(row[TableMappingMetadataColumns.SourceTable.ToString()].ToString())
                            .FirstOrDefault();
                        var targetFullyQualifiedName = MetadataHandling.GetSchema(row[TableMappingMetadataColumns.TargetTable.ToString()].ToString())
                            .FirstOrDefault();

                        _alert.SetTextLogging("--> Processing the " + sourceFullyQualifiedName.Value + " to " + targetFullyQualifiedName.Value + " relationship.\r\n");

                        var filterCriterion = row[TableMappingMetadataColumns.FilterCriterion.ToString()].ToString().Trim();
                        filterCriterion = filterCriterion.Replace("'", "''");

                        var businessKeyDefinition = row[TableMappingMetadataColumns.BusinessKeyDefinition.ToString()].ToString().Trim();
                        businessKeyDefinition = businessKeyDefinition.Replace("'", "''");

                        var insertStatement = new StringBuilder();
                        insertStatement.AppendLine("INSERT INTO [MD_SOURCE_STAGING_XREF]");
                        insertStatement.AppendLine("([SOURCE_NAME], [STAGING_NAME], [CHANGE_DATETIME_DEFINITION], [CHANGE_DATA_CAPTURE_DEFINITION], [KEY_DEFINITION], [FILTER_CRITERIA])");
                        insertStatement.AppendLine("VALUES (" +
                                                   "'" + sourceFullyQualifiedName.Value + "', " +
                                                   "'" + targetFullyQualifiedName.Value + "', " +
                                                   "NULL, " +
                                                   "NULL, " +
                                                   "'" + businessKeyDefinition + "', " +
                                                   "'" + filterCriterion + "'" +
                                                   ")");

                        var command = new SqlCommand(insertStatement.ToString(), connection);

                        try
                        {
                            connection.Open();
                            command.ExecuteNonQuery();
                        }
                        catch (Exception ex)
                        {
                            errorCounter++;
                            _alert.SetTextLogging(
                                "An issue has occured during preparation of the relationship between the Source and the Staging Area. Please check the Error Log for more details.\r\n");

                            errorLog.AppendLine(
                                "\r\nAn issue has occured during preparation of the Source / Staging Area XREF: \r\n\r\n" +
                                ex);
                            errorLog.AppendLine("\r\nThe query that caused the issue is: \r\n\r\n" + insertStatement);
                        }
                    }
                }

                worker?.ReportProgress(10);
                subProcess.Stop();
                _alert.SetTextLogging(
                    "Preparation of the Source / Staging Area XREF metadata completed, and has taken " +
                    subProcess.Elapsed.TotalSeconds + " seconds.\r\n");

                #endregion


                #region Prepare Persistent Staging Area - 13%

                //3. Prepare Persistent Staging Area
                subProcess.Reset();
                subProcess.Start();

                _alert.SetTextLogging("\r\n");
                _alert.SetTextLogging("Commencing preparing the Persistent Staging Area metadata.\r\n");

                // Getting the distinct list of tables to go into the MD_PERSISTENT_STAGING table
                if (TeamConfigurationSettings.TableNamingLocation == "Prefix")
                {
                    selectionRows = inputTableMetadata.Select(TableMappingMetadataColumns.Enabled.ToString() + " = 'true' AND " + TableMappingMetadataColumns.TargetTable.ToString() + " LIKE '" + TeamConfigurationSettings.PsaTablePrefixValue + "%'");
                }
                else
                {
                    selectionRows = inputTableMetadata.Select(TableMappingMetadataColumns.Enabled.ToString() + " = 'true' AND " + TableMappingMetadataColumns.TargetTable.ToString() + " LIKE '%" + TeamConfigurationSettings.PsaTablePrefixValue + "'");
                }

                var distinctListPsa = new List<string>
                {
                    // Create a dummy row
                    "Not applicable"
                };

                // Create a distinct list of sources from the data grid
                foreach (DataRow row in selectionRows)
                {
                    var target_table = row[TableMappingMetadataColumns.TargetTable.ToString()].ToString().Trim();
                    if (!distinctListPsa.Contains(target_table))
                    {
                        distinctListPsa.Add(target_table);
                    }
                }

                // Process the unique Persistent Staging Area records
                foreach (var tableName in distinctListPsa)
                {
                    using (var connection = new SqlConnection(metaDataConnection))
                    {
                        if (tableName != "Not applicable")
                        {
                            _alert.SetTextLogging("--> " + tableName + "\r\n");
                        }

                        var fullyQualifiedName = MetadataHandling.GetSchema(tableName).FirstOrDefault();

                        var insertStatement = new StringBuilder();
                        insertStatement.AppendLine("INSERT INTO [MD_PERSISTENT_STAGING]");
                        insertStatement.AppendLine("([PERSISTENT_STAGING_NAME], [PERSISTENT_STAGING_NAME_SHORT], [SCHEMA_NAME])");
                        insertStatement.AppendLine("VALUES ('" + tableName + "','" + fullyQualifiedName.Value + "','" + fullyQualifiedName.Key + "')");

                        var command = new SqlCommand(insertStatement.ToString(), connection);

                        try
                        {
                            connection.Open();
                            command.ExecuteNonQuery();
                        }
                        catch (Exception ex)
                        {
                            errorCounter++;
                            _alert.SetTextLogging("An issue has occured during preparation of the Persistent Staging Area. Please check the Error Log for more details.\r\n");

                            errorLog.AppendLine("\r\nAn issue has occured during preparation of the Persistent Staging Area: \r\n\r\n" + ex);
                            errorLog.AppendLine("\r\nThe query that caused the issue is: \r\n\r\n" + insertStatement);
                        }
                    }
                }

                if (worker != null) worker.ReportProgress(13);
                subProcess.Stop();
                _alert.SetTextLogging("Preparation of the Persistent Staging Area metadata completed, and has taken " + subProcess.Elapsed.TotalSeconds + " seconds.\r\n");

                #endregion


                #region Prepare Source to Persistent Staging Area XREF - 15%

                // Prepare the Source to Persistent Staging Area XREF
                subProcess.Reset();
                subProcess.Start();

                _alert.SetTextLogging("\r\n");
                _alert.SetTextLogging("Commencing preparing the relationship between Source and Persistent Staging Area.\r\n");

                // Getting the mapping list from the data table
                if (TeamConfigurationSettings.TableNamingLocation == "Prefix")
                {
                    selectionRows = inputTableMetadata.Select(TableMappingMetadataColumns.Enabled.ToString() + " = 'true' AND " + TableMappingMetadataColumns.TargetTable.ToString() + " LIKE '" + TeamConfigurationSettings.PsaTablePrefixValue + "%'");
                }
                else
                {
                    selectionRows = inputTableMetadata.Select(TableMappingMetadataColumns.Enabled.ToString() + " = 'true' AND " + TableMappingMetadataColumns.TargetTable.ToString() + " LIKE '%" + TeamConfigurationSettings.PsaTablePrefixValue + "'");
                }

                // Process the unique Staging Area records
                foreach (var row in selectionRows)
                {
                    using (var connection = new SqlConnection(metaDataConnection))
                    {
                        _alert.SetTextLogging("--> Processing the " + row[TableMappingMetadataColumns.SourceTable.ToString()] + " to " + row[TableMappingMetadataColumns.TargetTable.ToString()] + " relationship.\r\n");

                        var filterCriterion = row[TableMappingMetadataColumns.FilterCriterion.ToString()].ToString().Trim();
                        filterCriterion = filterCriterion.Replace("'", "''");

                        var businessKeyDefinition = row[TableMappingMetadataColumns.BusinessKeyDefinition.ToString()].ToString().Trim();
                        businessKeyDefinition = businessKeyDefinition.Replace("'", "''");

                        var insertStatement = new StringBuilder();
                        insertStatement.AppendLine("INSERT INTO [MD_SOURCE_PERSISTENT_STAGING_XREF]");
                        insertStatement.AppendLine("([SOURCE_NAME], [PERSISTENT_STAGING_NAME], [CHANGE_DATETIME_DEFINITION], [KEY_DEFINITION], [FILTER_CRITERIA])");
                        insertStatement.AppendLine("VALUES ('" + row[TableMappingMetadataColumns.SourceTable.ToString()] + "','" +
                                                   row[TableMappingMetadataColumns.TargetTable.ToString()] + 
                                                   "', NULL, '" +
                                                   businessKeyDefinition + "', '" + 
                                                   filterCriterion + "')");

                        var command = new SqlCommand(insertStatement.ToString(), connection);

                        try
                        {
                            connection.Open();
                            command.ExecuteNonQuery();
                        }
                        catch (Exception ex)
                        {
                            errorCounter++;
                            _alert.SetTextLogging(
                                "An issue has occured during preparation of the relationship between the Source and the Persistent Staging Area. Please check the Error Log for more details.\r\n");

                            errorLog.AppendLine(
                                "\r\nAn issue has occured during preparation of the Source / Persistent Staging Area XREF: \r\n\r\n" +
                                ex);
                            errorLog.AppendLine("\r\nThe query that caused the issue is: \r\n\r\n" + insertStatement);
                        }
                    }
                }

                worker?.ReportProgress(15);
                subProcess.Stop();
                _alert.SetTextLogging(
                    "Preparation of the Source / Persistent Staging Area XREF metadata completed, and has taken " +
                    subProcess.Elapsed.TotalSeconds + " seconds.\r\n");

                #endregion


                #region Prepare Hubs - 17%

                //3. Prepare Hubs
                subProcess.Reset();
                subProcess.Start();

                _alert.SetTextLogging("\r\n");
                _alert.SetTextLogging("Commencing preparing the Hub metadata.\r\n");

                // Getting the distinct list of tables to go into the MD_HUB table
                selectionRows =
                    inputTableMetadata.Select(
                        TableMappingMetadataColumns.Enabled.ToString() + " = 'true' AND " + TableMappingMetadataColumns.TargetTable.ToString() + " LIKE '%" + hubTablePrefix + "%'");

                var distinctListHub = new List<string>();

                // Create a dummy row
                distinctListHub.Add("Not applicable");

                // Create a distinct list of sources from the datagrid
                foreach (DataRow row in selectionRows)
                {
                    string target_table = row[TableMappingMetadataColumns.TargetTable.ToString()].ToString().Trim();
                    if (!distinctListHub.Contains(target_table))
                    {
                        distinctListHub.Add(target_table);
                    }
                }

                // Process the unique Hub records
                foreach (var tableName in distinctListHub)
                {
                    var hubBusinessKey = new List<string>();

                    using (var connection = new SqlConnection(metaDataConnection))
                    {
                        if (tableName != "Not applicable")
                        {
                            _alert.SetTextLogging("--> " + tableName + "\r\n");

                            // Retrieve the business key
                            if (queryMode == "physical")
                            {
                                if (!localTableMappingConnectionDictionary.TryGetValue(tableName, out var connectionValue))
                                {
                                    // the key isn't in the dictionary.
                                    GlobalParameters.TeamEventLog.Add(Event.CreateNewEvent(EventTypes.Error, $"The connection string for {tableName} could not be found."));
                                }

                                hubBusinessKey = MetadataHandling.GetHubTargetBusinessKeyListPhysical(tableName, connectionValue, TeamConfigurationSettings);
                            }
                            else
                            {
                                hubBusinessKey = MetadataHandling.GetHubTargetBusinessKeyListVirtual(tableName, versionId, TeamConfigurationSettings);
                            }
                        }

                        var fullyQualifiedName = MetadataHandling.GetSchema(tableName).FirstOrDefault();

                        string businessKeyString = string.Join(",", hubBusinessKey);
                        string surrogateKey = MetadataHandling.GetSurrogateKey(tableName, TeamConfigurationSettings);

                        var insertStatement = new StringBuilder();
                        insertStatement.AppendLine("INSERT INTO [MD_HUB]");
                        insertStatement.AppendLine("([HUB_NAME], [SCHEMA_NAME], [BUSINESS_KEY], [SURROGATE_KEY])");
                        insertStatement.AppendLine("VALUES ('" + fullyQualifiedName.Value + "','" + fullyQualifiedName.Key + "', '" + businessKeyString + "', '" + surrogateKey + "')");

                        var command = new SqlCommand(insertStatement.ToString(), connection);

                        try
                        {
                            connection.Open();
                            command.ExecuteNonQuery();
                        }
                        catch (Exception ex)
                        {
                            errorCounter++;
                            _alert.SetTextLogging(
                                "An issue has occured during preparation of the Hubs. Please check the Error Log for more details.\r\n");

                            errorLog.AppendLine(
                                "\r\nAn issue has occured during preparation of the Hubs: \r\n\r\n" + ex);
                            errorLog.AppendLine("\r\nThe query that caused the issue is: \r\n\r\n" + insertStatement);
                        }
                    }
                }

                if (worker != null) worker.ReportProgress(17);
                subProcess.Stop();
                _alert.SetTextLogging("Preparation of the Hub metadata completed, and has taken " +
                                      subProcess.Elapsed.TotalSeconds + " seconds.\r\n");

                #endregion


                #region Prepare Links - 20%

                //4. Prepare links
                subProcess.Reset();
                subProcess.Start();

                _alert.SetTextLogging("\r\n");
                _alert.SetTextLogging("Commencing preparing the Link metadata.\r\n");

                // Getting the distinct list of tables to go into the MD_LINK table
                selectionRows = inputTableMetadata.Select(TableMappingMetadataColumns.Enabled.ToString() + " = 'true' AND " + TableMappingMetadataColumns.TargetTable.ToString() + " LIKE '%" + lnkTablePrefix + "%'");

                var distinctListLinks = new List<string>();

                // Create a dummy row
                distinctListLinks.Add("Not applicable");

                // Create a distinct list of sources from the data grid
                foreach (DataRow row in selectionRows)
                {
                    string target_table = row[TableMappingMetadataColumns.TargetTable.ToString()].ToString().Trim();
                    if (!distinctListLinks.Contains(target_table))
                    {
                        distinctListLinks.Add(target_table);
                    }
                }

                // Insert the rest of the rows
                foreach (var tableName in distinctListLinks)
                {
                    using (var connection = new SqlConnection(metaDataConnection))
                    {
                        if (tableName != "Not applicable")
                        {
                            _alert.SetTextLogging("--> " + tableName + "\r\n");
                        }

                        var fullyQualifiedName = MetadataHandling.GetSchema(tableName).FirstOrDefault();

                        // Retrieve the surrogate key
                        string surrogateKey = MetadataHandling.GetSurrogateKey(tableName, TeamConfigurationSettings);

                        var insertStatement = new StringBuilder();

                        insertStatement.AppendLine("INSERT INTO [MD_LINK]");
                        insertStatement.AppendLine("([LINK_NAME], [SCHEMA_NAME], [SURROGATE_KEY])");
                        insertStatement.AppendLine("VALUES ('" + fullyQualifiedName.Value + "','" + fullyQualifiedName.Key + "','" + surrogateKey + "')");

                        var command = new SqlCommand(insertStatement.ToString(), connection);

                        try
                        {
                            connection.Open();
                            command.ExecuteNonQuery();
                        }
                        catch (Exception ex)
                        {
                            errorCounter++;
                            _alert.SetTextLogging(
                                "An issue has occured during preparation of the Links. Please check the Error Log for more details.\r\n");

                            errorLog.AppendLine("\r\nAn issue has occured during preparation of the Links: \r\n\r\n" +
                                                ex);
                            errorLog.AppendLine("\r\nThe query that caused the issue is: \r\n\r\n" + insertStatement);
                        }
                    }
                }

                if (worker != null) worker.ReportProgress(20);
                subProcess.Stop();
                _alert.SetTextLogging("Preparation of the Link metadata completed, and has taken " + subProcess.Elapsed.TotalSeconds + " seconds.\r\n");

                #endregion


                #region Prepare Satellites - 24%

                // Prepare Satellites
                subProcess.Reset();
                subProcess.Start();

                _alert.SetTextLogging("\r\n");
                _alert.SetTextLogging("Commencing preparing the Satellite metadata.\r\n");

                var prepareSatStatement = new StringBuilder();

                prepareSatStatement.AppendLine("SELECT DISTINCT");
                prepareSatStatement.AppendLine("  spec.TARGET_TABLE AS SATELLITE_NAME,");
                prepareSatStatement.AppendLine("  hubkeysub.HUB_NAME, ");
                prepareSatStatement.AppendLine("  'Normal' AS SATELLITE_TYPE, ");
                prepareSatStatement.AppendLine("  (SELECT LINK_NAME FROM MD_LINK WHERE LINK_NAME='Not applicable') AS LINK_NAME -- No link for normal Satellites ");
                prepareSatStatement.AppendLine("FROM TMP_MD_TABLE_MAPPING spec ");
                prepareSatStatement.AppendLine("LEFT OUTER JOIN ");
                prepareSatStatement.AppendLine("(");
                prepareSatStatement.AppendLine("  SELECT DISTINCT TARGET_TABLE, hub.HUB_NAME, SOURCE_TABLE, BUSINESS_KEY_ATTRIBUTE ");
                prepareSatStatement.AppendLine("  FROM TMP_MD_TABLE_MAPPING spec2 ");
                prepareSatStatement.AppendLine("  LEFT OUTER JOIN -- Join in the Hub NAME from the MD table ");
                prepareSatStatement.AppendLine("  MD_HUB hub ON hub.[SCHEMA_NAME]+'.'+hub.HUB_NAME=spec2.TARGET_TABLE ");
                prepareSatStatement.AppendLine("  WHERE TARGET_TABLE_TYPE = '" + MetadataHandling.TableTypes.CoreBusinessConcept + "' AND [ENABLED_INDICATOR] = 'True'                                                        ");
                prepareSatStatement.AppendLine(") hubkeysub ");
                prepareSatStatement.AppendLine("        ON spec.SOURCE_TABLE=hubkeysub.SOURCE_TABLE ");
                prepareSatStatement.AppendLine("        AND replace(spec.BUSINESS_KEY_ATTRIBUTE,' ','')=replace(hubkeysub.BUSINESS_KEY_ATTRIBUTE,' ','') ");
                prepareSatStatement.AppendLine("WHERE spec.TARGET_TABLE_TYPE = '" + MetadataHandling.TableTypes.Context + "' ");
                prepareSatStatement.AppendLine("AND [ENABLED_INDICATOR] = 'True'");

                var listSat = Utility.GetDataTable(ref connOmd, prepareSatStatement.ToString());

                foreach (DataRow satelliteName in listSat.Rows)
                {
                    using (var connection = new SqlConnection(metaDataConnection))
                    {
                        var tableName = satelliteName["SATELLITE_NAME"].ToString().Trim();
                        var tableType = satelliteName["SATELLITE_TYPE"].ToString().Trim();
                        var hubName = satelliteName["HUB_NAME"];
                        var linkName = satelliteName["LINK_NAME"];

                        var fullyQualifiedName = MetadataHandling.GetSchema(tableName).FirstOrDefault();

                        if (tableName != "Not applicable")
                        {
                            _alert.SetTextLogging("--> " + fullyQualifiedName.Value + "\r\n");
                        }

                        var insertStatement = new StringBuilder();
                        insertStatement.AppendLine("INSERT INTO [MD_SATELLITE]");
                        insertStatement.AppendLine(
                            "([SATELLITE_NAME], [SATELLITE_TYPE], [SCHEMA_NAME], [HUB_NAME], [LINK_NAME])");
                        insertStatement.AppendLine("VALUES ('" + fullyQualifiedName.Value + "','" + tableType + "', '" +
                                                   fullyQualifiedName.Key + "', '" + hubName + "','" + linkName + "')");

                        var command = new SqlCommand(insertStatement.ToString(), connection);

                        try
                        {
                            connection.Open();
                            command.ExecuteNonQuery();
                        }
                        catch (Exception ex)
                        {
                            errorCounter++;
                            _alert.SetTextLogging(
                                "An issue has occured during preparation of the Satellites. Please check the Error Log for more details.\r\n");

                            errorLog.AppendLine(
                                "\r\nAn issue has occured during preparation of the Satellites: \r\n\r\n" + ex);
                            errorLog.AppendLine("\r\nThe query that caused the issue is: \r\n\r\n" + insertStatement);
                        }
                    }
                }

                worker.ReportProgress(24);
                subProcess.Stop();
                _alert.SetTextLogging("Preparation of the Satellite metadata completed, and has taken " +
                                      subProcess.Elapsed.TotalSeconds + " seconds.\r\n");

                #endregion


                #region Prepare Link Satellites - 28%

                //Prepare Link Satellites
                subProcess.Reset();
                subProcess.Start();

                _alert.SetTextLogging("\r\n");
                _alert.SetTextLogging("Commencing preparing the Link Satellite metadata.\r\n");

                var prepareLsatStatement = new StringBuilder();
                prepareLsatStatement.AppendLine("SELECT DISTINCT");
                prepareLsatStatement.AppendLine("        spec.TARGET_TABLE AS SATELLITE_NAME, ");
                prepareLsatStatement.AppendLine(
                    "        (SELECT HUB_NAME FROM MD_HUB WHERE HUB_NAME='Not applicable') AS HUB_NAME, -- No Hub for Link Satellites");
                prepareLsatStatement.AppendLine("        'Link Satellite' AS SATELLITE_TYPE,");
                prepareLsatStatement.AppendLine("        lnkkeysub.LINK_NAME");
                prepareLsatStatement.AppendLine("FROM TMP_MD_TABLE_MAPPING spec");
                prepareLsatStatement.AppendLine("LEFT OUTER JOIN  -- Get the Link ID that belongs to this LSAT");
                prepareLsatStatement.AppendLine("(");
                prepareLsatStatement.AppendLine("        SELECT DISTINCT ");
                prepareLsatStatement.AppendLine("                lnk.LINK_NAME AS LINK_NAME,");
                prepareLsatStatement.AppendLine("                SOURCE_TABLE,");
                prepareLsatStatement.AppendLine("                BUSINESS_KEY_ATTRIBUTE");
                prepareLsatStatement.AppendLine("        FROM TMP_MD_TABLE_MAPPING spec2");
                prepareLsatStatement.AppendLine("        LEFT OUTER JOIN -- Join in the Link Name from the MD table");
                prepareLsatStatement.AppendLine(
                    "                MD_LINK lnk ON lnk.[SCHEMA_NAME]+'.'+lnk.LINK_NAME=spec2.TARGET_TABLE");
                prepareLsatStatement.AppendLine("        WHERE TARGET_TABLE_TYPE = '" +
                                                MetadataHandling.TableTypes.NaturalBusinessRelationship + "' ");
                prepareLsatStatement.AppendLine("        AND [ENABLED_INDICATOR] = 'True'");
                prepareLsatStatement.AppendLine(") lnkkeysub");
                prepareLsatStatement.AppendLine(
                    "    ON spec.SOURCE_TABLE=lnkkeysub.SOURCE_TABLE -- Only the combination of Link table and Business key can belong to the LSAT");
                prepareLsatStatement.AppendLine(
                    "    AND REPLACE(spec.BUSINESS_KEY_ATTRIBUTE,' ','')=REPLACE(lnkkeysub.BUSINESS_KEY_ATTRIBUTE,' ','')");
                prepareLsatStatement.AppendLine(
                    "-- Only select Link Satellites as the base / driving table (spec alias)");
                prepareLsatStatement.AppendLine("WHERE spec.TARGET_TABLE_TYPE = '" +
                                                MetadataHandling.TableTypes.NaturalBusinessRelationshipContext +
                                                "'");
                prepareLsatStatement.AppendLine("AND [ENABLED_INDICATOR] = 'True'");


                var listLsat = Utility.GetDataTable(ref connOmd, prepareLsatStatement.ToString());

                foreach (DataRow satelliteName in listLsat.Rows)
                {
                    using (var connection = new SqlConnection(metaDataConnection))
                    {
                        var tableName = satelliteName["SATELLITE_NAME"].ToString().Trim();
                        var tableType = satelliteName["SATELLITE_TYPE"].ToString().Trim();
                        var hubName = satelliteName["HUB_NAME"];
                        var linkName = satelliteName["LINK_NAME"];

                        var fullyQualifiedName = MetadataHandling.GetSchema(tableName).FirstOrDefault();

                        _alert.SetTextLogging("--> " + fullyQualifiedName.Value + "\r\n");

                        var insertStatement = new StringBuilder();
                        insertStatement.AppendLine("INSERT INTO [MD_SATELLITE]");
                        insertStatement.AppendLine(
                            "([SATELLITE_NAME], [SATELLITE_TYPE], [SCHEMA_NAME], [HUB_NAME], [LINK_NAME])");
                        insertStatement.AppendLine("VALUES ('" + fullyQualifiedName.Value + "','" + tableType + "', '" +
                                                   fullyQualifiedName.Key + "', '" + hubName + "','" + linkName + "')");

                        var command = new SqlCommand(insertStatement.ToString(), connection);

                        try
                        {
                            connection.Open();
                            command.ExecuteNonQuery();
                        }
                        catch (Exception ex)
                        {
                            errorCounter++;
                            _alert.SetTextLogging(
                                "An issue has occured during preparation of the Link Satellites. Please check the Error Log for more details.\r\n");

                            errorLog.AppendLine(
                                "\r\nAn issue has occured during preparation of the Link Satellites: \r\n\r\n" + ex);
                            errorLog.AppendLine("\r\nThe query that caused the issue is: \r\n\r\n" + insertStatement);

                        }
                    }
                }

                worker.ReportProgress(28);
                subProcess.Stop();
                _alert.SetTextLogging("Preparation of the Link Satellite metadata completed, and has taken " +
                                      subProcess.Elapsed.TotalSeconds + " seconds.\r\n");

                #endregion


                #region Prepare Source / SAT Xref - 28%

                //Prepare Source / Sat XREF
                subProcess.Reset();
                subProcess.Start();

                _alert.SetTextLogging("\r\n");
                _alert.SetTextLogging(
                    "Commencing preparing the relationship between (Link) Satellites and the Source tables.\r\n");

                var prepareSatXrefStatement = new StringBuilder();
                prepareSatXrefStatement.AppendLine("SELECT");
                prepareSatXrefStatement.AppendLine("        sat.SATELLITE_NAME,");
                prepareSatXrefStatement.AppendLine("        stg.SOURCE_NAME,");
                prepareSatXrefStatement.AppendLine("        spec.BUSINESS_KEY_ATTRIBUTE,");
                prepareSatXrefStatement.AppendLine("        spec.FILTER_CRITERIA");
                prepareSatXrefStatement.AppendLine("FROM TMP_MD_TABLE_MAPPING spec");
                prepareSatXrefStatement.AppendLine("LEFT OUTER JOIN -- Join in the Source_ID from the MD_SOURCE table");
                prepareSatXrefStatement.AppendLine(
                    "        MD_SOURCE stg ON stg.[SCHEMA_NAME]+'.'+stg.SOURCE_NAME=spec.SOURCE_TABLE");
                prepareSatXrefStatement.AppendLine(
                    "LEFT OUTER JOIN -- Join in the Satellite_ID from the MD_SATELLITE table");
                prepareSatXrefStatement.AppendLine(
                    "        MD_SATELLITE sat ON sat.[SCHEMA_NAME]+'.'+sat.SATELLITE_NAME=spec.TARGET_TABLE");
                prepareSatXrefStatement.AppendLine("WHERE spec.TARGET_TABLE_TYPE = '" +
                                                   MetadataHandling.TableTypes.Context + "'");
                prepareSatXrefStatement.AppendLine("AND [ENABLED_INDICATOR] = 'True'");
                prepareSatXrefStatement.AppendLine("UNION");
                prepareSatXrefStatement.AppendLine("SELECT");
                prepareSatXrefStatement.AppendLine("        sat.SATELLITE_NAME,");
                prepareSatXrefStatement.AppendLine("        stg.SOURCE_NAME,");
                prepareSatXrefStatement.AppendLine("        spec.BUSINESS_KEY_ATTRIBUTE,");
                prepareSatXrefStatement.AppendLine("        spec.FILTER_CRITERIA");
                prepareSatXrefStatement.AppendLine("FROM TMP_MD_TABLE_MAPPING spec");
                prepareSatXrefStatement.AppendLine("LEFT OUTER JOIN -- Join in the Source from the MD_SOURCE table");
                prepareSatXrefStatement.AppendLine(
                    "        MD_SOURCE stg ON stg.[SCHEMA_NAME]+'.'+stg.SOURCE_NAME=spec.SOURCE_TABLE");
                prepareSatXrefStatement.AppendLine(
                    "LEFT OUTER JOIN -- Join in the Satellite_ID from the MD_SATELLITE table");
                prepareSatXrefStatement.AppendLine(
                    "        MD_SATELLITE sat ON sat.[SCHEMA_NAME]+'.'+sat.SATELLITE_NAME=spec.TARGET_TABLE");
                prepareSatXrefStatement.AppendLine("WHERE spec.TARGET_TABLE_TYPE = '" +
                                                   MetadataHandling.TableTypes.NaturalBusinessRelationshipContext +
                                                   "'");
                prepareSatXrefStatement.AppendLine("AND [ENABLED_INDICATOR] = 'True'");

                var listSatXref = Utility.GetDataTable(ref connOmd, prepareSatXrefStatement.ToString());

                foreach (DataRow tableName in listSatXref.Rows)
                {
                    using (var connection = new SqlConnection(metaDataConnection))
                    {
                        _alert.SetTextLogging("--> Processing the " + tableName["SOURCE_NAME"] + " to " +
                                              tableName["SATELLITE_NAME"] + " relationship.\r\n");

                        var insertStatement = new StringBuilder();
                        var filterCriterion = tableName["FILTER_CRITERIA"].ToString().Trim();
                        filterCriterion = filterCriterion.Replace("'", "''");

                        var businessKeyDefinition = tableName["BUSINESS_KEY_ATTRIBUTE"].ToString().Trim();
                        businessKeyDefinition = businessKeyDefinition.Replace("'", "''");

                        var loadVector = MetadataHandling.GetLoadVector(tableName["SOURCE_NAME"].ToString(),
                            tableName["SATELLITE_NAME"].ToString(), TeamConfigurationSettings);

                        insertStatement.AppendLine("INSERT INTO [MD_SOURCE_SATELLITE_XREF]");
                        insertStatement.AppendLine(
                            "([SATELLITE_NAME], [SOURCE_NAME], [BUSINESS_KEY_DEFINITION], [FILTER_CRITERIA], [LOAD_VECTOR])");
                        insertStatement.AppendLine("VALUES ('" +
                                                   tableName["SATELLITE_NAME"] + "','" +
                                                   tableName["SOURCE_NAME"] + "','" +
                                                   businessKeyDefinition + "','" +
                                                   filterCriterion + "','" +
                                                   loadVector + "')");

                        var command = new SqlCommand(insertStatement.ToString(), connection);

                        try
                        {
                            connection.Open();
                            command.ExecuteNonQuery();
                        }
                        catch (Exception ex)
                        {
                            errorCounter++;
                            _alert.SetTextLogging(
                                "An issue has occured during preparation of the relationship between the Source and the Satellite. Please check the Error Log for more details.\r\n");

                            errorLog.AppendLine(
                                "\r\nAn issue has occured during preparation of the Source / Satellite XREF: \r\n\r\n" +
                                ex);
                            errorLog.AppendLine("\r\nThe query that caused the issue is: \r\n\r\n" + insertStatement);
                        }
                    }
                }

                worker.ReportProgress(28);
                subProcess.Stop();
                _alert.SetTextLogging("Preparation of the Source / Satellite XREF metadata completed, and has taken " +
                                      subProcess.Elapsed.TotalSeconds + " seconds.\r\n");

                #endregion


                #region Source / Hub relationship - 30%

                //Prepare Source / HUB xref
                subProcess.Reset();
                subProcess.Start();

                _alert.SetTextLogging("\r\n");
                _alert.SetTextLogging("Commencing preparing the relationship between Source and Hubs.\r\n");

                var prepareStgHubXrefStatement = new StringBuilder();
                prepareStgHubXrefStatement.AppendLine("SELECT");
                prepareStgHubXrefStatement.AppendLine("    HUB_NAME,");
                prepareStgHubXrefStatement.AppendLine("    SOURCE_NAME,");
                prepareStgHubXrefStatement.AppendLine("    BUSINESS_KEY_ATTRIBUTE,");
                prepareStgHubXrefStatement.AppendLine("    FILTER_CRITERIA");
                prepareStgHubXrefStatement.AppendLine("FROM");
                prepareStgHubXrefStatement.AppendLine("(      ");
                prepareStgHubXrefStatement.AppendLine("    SELECT DISTINCT ");
                prepareStgHubXrefStatement.AppendLine("    SOURCE_TABLE,");
                prepareStgHubXrefStatement.AppendLine("    TARGET_TABLE,");
                prepareStgHubXrefStatement.AppendLine("    BUSINESS_KEY_ATTRIBUTE,");
                prepareStgHubXrefStatement.AppendLine("    FILTER_CRITERIA");
                prepareStgHubXrefStatement.AppendLine("    FROM TMP_MD_TABLE_MAPPING");
                prepareStgHubXrefStatement.AppendLine("    WHERE ");
                prepareStgHubXrefStatement.AppendLine("        TARGET_TABLE_TYPE = '" +
                                                      MetadataHandling.TableTypes.CoreBusinessConcept + "'");
                prepareStgHubXrefStatement.AppendLine("    AND [ENABLED_INDICATOR] = 'True'");
                prepareStgHubXrefStatement.AppendLine(") hub");
                prepareStgHubXrefStatement.AppendLine("LEFT OUTER JOIN");
                prepareStgHubXrefStatement.AppendLine("( ");
                prepareStgHubXrefStatement.AppendLine("    SELECT SOURCE_NAME, [SCHEMA_NAME]");
                prepareStgHubXrefStatement.AppendLine("    FROM MD_SOURCE");
                prepareStgHubXrefStatement.AppendLine(") stgsub");
                prepareStgHubXrefStatement.AppendLine(
                    "ON hub.SOURCE_TABLE=stgsub.[SCHEMA_NAME]+'.'+stgsub.SOURCE_NAME");
                prepareStgHubXrefStatement.AppendLine("LEFT OUTER JOIN");
                prepareStgHubXrefStatement.AppendLine("( ");
                prepareStgHubXrefStatement.AppendLine("    SELECT HUB_NAME, [SCHEMA_NAME]");
                prepareStgHubXrefStatement.AppendLine("    FROM MD_HUB");
                prepareStgHubXrefStatement.AppendLine(") hubsub");
                prepareStgHubXrefStatement.AppendLine("ON hub.TARGET_TABLE=hubsub.[SCHEMA_NAME]+'.'+hubsub.HUB_NAME");

                var listXref = Utility.GetDataTable(ref connOmd, prepareStgHubXrefStatement.ToString());

                foreach (DataRow tableName in listXref.Rows)
                {
                    using (var connection = new SqlConnection(metaDataConnection))
                    {
                        _alert.SetTextLogging("--> Processing the " + tableName["SOURCE_NAME"] + " to " +
                                              tableName["HUB_NAME"] + " relationship.\r\n");

                        var filterCriterion = tableName["FILTER_CRITERIA"].ToString().Trim();
                        filterCriterion = filterCriterion.Replace("'", "''");

                        var businessKeyDefinition = tableName["BUSINESS_KEY_ATTRIBUTE"].ToString().Trim();
                        businessKeyDefinition = businessKeyDefinition.Replace("'", "''");

                        var loadVector = MetadataHandling.GetLoadVector(tableName["SOURCE_NAME"].ToString(),
                            tableName["HUB_NAME"].ToString(), TeamConfigurationSettings);

                        var insertStatement = new StringBuilder();
                        insertStatement.AppendLine("INSERT INTO [MD_SOURCE_HUB_XREF]");
                        insertStatement.AppendLine(
                            "([HUB_NAME], [SOURCE_NAME], [BUSINESS_KEY_DEFINITION], [FILTER_CRITERIA], [LOAD_VECTOR])");
                        insertStatement.AppendLine("VALUES ('" + tableName["HUB_NAME"] +
                                                   "','" + tableName["SOURCE_NAME"] +
                                                   "','" + businessKeyDefinition +
                                                   "','" + filterCriterion +
                                                   "','" + loadVector +
                                                   "')");

                        var command = new SqlCommand(insertStatement.ToString(), connection);

                        try
                        {
                            connection.Open();
                            command.ExecuteNonQuery();
                        }
                        catch (Exception ex)
                        {
                            errorCounter++;
                            _alert.SetTextLogging(
                                "An issue has occured during preparation of the relationship between the Source and the Hubs. Please check the Error Log for more details.\r\n");

                            errorLog.AppendLine(
                                "\r\nAn issue has occured during preparation of the Staging / Hub XREF: \r\n\r\n" + ex);
                            errorLog.AppendLine("\r\nThe query that caused the issue is: \r\n\r\n" + insertStatement);
                        }
                    }
                }

                worker.ReportProgress(30);
                subProcess.Stop();
                _alert.SetTextLogging(
                    "Preparation of the relationship between Source and Hubs completed, and has taken " +
                    subProcess.Elapsed.TotalSeconds + " seconds.\r\n");

                #endregion


                #region Physical Model dump- 40%

                // Creating a point-in-time snapshot of the physical model used for export to the interface schemas
                subProcess.Reset();
                subProcess.Start();

                _alert.SetTextLogging("\r\n");
                _alert.SetTextLogging("Creating a snapshot of the physical model.\r\n");

                // First, define the master attribute list for reuse many times later on (assuming ignore version is active and hence the virtual mode is enabled).
                var physicalModelDataTable = new DataTable();

                if (radioButtonPhysicalMode.Checked) // Get the attributes from the physical model / catalog. No virtualisation needed.
                {
                    var physicalModelInstantiation = new AttributeSelection();

                    foreach (var connection in TeamConfigurationSettings.ConnectionDictionary)
                    {
                        if (connection.Key != "Metadata")
                        {
                            var localConnectionObject = (TeamConnection) connection.Value;
                            var localSqlConnection = new SqlConnection {ConnectionString = localConnectionObject.CreateSqlServerConnectionString(false)};

                            // Build up the filter criteria to only select information for tables that are associated with the connection
                            var tableFilterObjects = "";
                            foreach (DataGridViewRow row in dataGridViewTableMetadata.Rows)
                            {
                                if (row.IsNewRow == false)
                                {
                                    if (row.Cells[TableMappingMetadataColumns.SourceConnection.ToString()].Value.ToString() ==
                                        connection.Value.ConnectionInternalId)
                                    {
                                        var localTable = row.Cells[TableMappingMetadataColumns.SourceTable.ToString()].Value.ToString();
                                        localTable = MetadataHandling.GetFullyQualifiedTableName(localTable);
                                        tableFilterObjects =
                                            tableFilterObjects + "OBJECT_ID(N'[" +
                                            connection.Value.DatabaseServer.DatabaseName + "]." + localTable + "') ,";
                                    }

                                    if (row.Cells[TableMappingMetadataColumns.TargetConnection.ToString()].Value.ToString() ==
                                        connection.Value.ConnectionInternalId)
                                    {
                                        var localTable = row.Cells[TableMappingMetadataColumns.TargetTable.ToString()].Value.ToString();
                                        localTable = MetadataHandling.GetFullyQualifiedTableName(localTable);
                                        tableFilterObjects =
                                            tableFilterObjects + "OBJECT_ID(N'[" +
                                            connection.Value.DatabaseServer.DatabaseName + "]." + localTable + "') ,";
                                    }
                                }
                            }
                            tableFilterObjects = tableFilterObjects.TrimEnd(',');


                            var physicalModelStatement = new StringBuilder();
                            physicalModelStatement.AppendLine("SELECT ");
                            physicalModelStatement.AppendLine(" [DATABASE_NAME] ");
                            physicalModelStatement.AppendLine(",[SCHEMA_NAME]");
                            physicalModelStatement.AppendLine(",[TABLE_NAME]");
                            physicalModelStatement.AppendLine(",[COLUMN_NAME]");
                            physicalModelStatement.AppendLine(",[DATA_TYPE]");
                            physicalModelStatement.AppendLine(",[CHARACTER_MAXIMUM_LENGTH]");
                            physicalModelStatement.AppendLine(",[NUMERIC_PRECISION]");
                            physicalModelStatement.AppendLine(",[ORDINAL_POSITION]");
                            physicalModelStatement.AppendLine(",[PRIMARY_KEY_INDICATOR]");
                            physicalModelStatement.AppendLine("FROM");
                            physicalModelStatement.AppendLine("(");
                            physicalModelStatement.AppendLine(physicalModelInstantiation.CreatePhysicalModelSet(localConnectionObject.DatabaseServer.DatabaseName, tableFilterObjects)
                                .ToString());
                            physicalModelStatement.AppendLine(") sub");

                            var localPhysicalModelDataTable = Utility.GetDataTable(ref localSqlConnection, physicalModelStatement.ToString());

                            if (localPhysicalModelDataTable != null)
                            {
                                physicalModelDataTable.Merge(localPhysicalModelDataTable);
                            }
                        }
                    }
                }
                else // Get the values from the data grid or worker table (virtual mode)
                {
                    StringBuilder allVirtualDatabaseAttributes = new StringBuilder();

                    allVirtualDatabaseAttributes.AppendLine("SELECT ");
                    allVirtualDatabaseAttributes.AppendLine("  [DATABASE_NAME] ");
                    allVirtualDatabaseAttributes.AppendLine(" ,[SCHEMA_NAME]");
                    allVirtualDatabaseAttributes.AppendLine(" ,[TABLE_NAME]");
                    allVirtualDatabaseAttributes.AppendLine(" ,[COLUMN_NAME]");
                    allVirtualDatabaseAttributes.AppendLine(" ,[DATA_TYPE]");
                    allVirtualDatabaseAttributes.AppendLine(" ,[CHARACTER_MAXIMUM_LENGTH]");
                    allVirtualDatabaseAttributes.AppendLine(" ,[NUMERIC_PRECISION]");
                    allVirtualDatabaseAttributes.AppendLine(" ,[ORDINAL_POSITION]");
                    allVirtualDatabaseAttributes.AppendLine(" ,[PRIMARY_KEY_INDICATOR]");
                    allVirtualDatabaseAttributes.AppendLine("FROM [TMP_MD_VERSION_ATTRIBUTE] mapping");

                    physicalModelDataTable = Utility.GetDataTable(ref connOmd, allVirtualDatabaseAttributes.ToString());
                }

                try
                {
                    if (physicalModelDataTable.Rows.Count == 0)
                    {
                        _alert.SetTextLogging("--> No model information was found in the metadata.\r\n");
                    }
                    else
                    {
                        // Create a large insert string to save per-row database connection.
                        var createStatement = new StringBuilder();

                        foreach (DataRow tableName in physicalModelDataTable.Rows)
                        {
                            var insertKeyStatement = new StringBuilder();

                            insertKeyStatement.AppendLine("INSERT INTO [MD_PHYSICAL_MODEL]");
                            insertKeyStatement.AppendLine("([DATABASE_NAME], " +
                                                          "[SCHEMA_NAME], " +
                                                          "[TABLE_NAME], " +
                                                          "[COLUMN_NAME], " +
                                                          "[DATA_TYPE], " +
                                                          "[CHARACTER_MAXIMUM_LENGTH], " +
                                                          "[NUMERIC_PRECISION], " +
                                                          "[ORDINAL_POSITION], " +
                                                          "[PRIMARY_KEY_INDICATOR])");
                            insertKeyStatement.AppendLine("VALUES ('" +
                                                          tableName["DATABASE_NAME"].ToString().Trim() +
                                                          "','" + tableName["SCHEMA_NAME"].ToString().Trim() +
                                                          "','" + tableName["TABLE_NAME"].ToString().Trim() +
                                                          "','" + tableName["COLUMN_NAME"].ToString().Trim() +
                                                          "','" + tableName["DATA_TYPE"].ToString().Trim() +
                                                          "','" + tableName["CHARACTER_MAXIMUM_LENGTH"].ToString()
                                                              .Trim() +
                                                          "','" + tableName["NUMERIC_PRECISION"].ToString().Trim() +
                                                          "','" + tableName["ORDINAL_POSITION"].ToString().Trim() +
                                                          "','" + tableName["PRIMARY_KEY_INDICATOR"].ToString().Trim() +
                                                          "')");

                            createStatement.AppendLine(insertKeyStatement.ToString());
                        }

                        using (var connection = new SqlConnection(metaDataConnection))
                        {
                            // Execute the statement
                            var command = new SqlCommand(createStatement.ToString(), connection);

                            try
                            {
                                connection.Open();
                                command.ExecuteNonQuery();
                            }
                            catch (Exception ex)
                            {
                                errorCounter++;
                                _alert.SetTextLogging("An issue has occured during preparation of the physical model extract metadata. Please check the Error Log for more details.\r\n");
                                errorLog.AppendLine("\r\nAn issue has occured during preparation of physical model metadata: \r\n\r\n" + ex);
                            }
                        }
                    }

                    worker.ReportProgress(40);
                    subProcess.Stop();
                    _alert.SetTextLogging("Preparation of the physical model extract completed, and has taken " + subProcess.Elapsed.TotalSeconds + " seconds.\r\n");
                }
                catch (Exception ex)
                {
                    errorCounter++;
                    _alert.SetTextLogging("An issue has occured during preparation of the physical model metadata. Please check the Error Log for more details.\r\n");
                    errorLog.AppendLine("\r\nAn issue has occured during preparation of physical model metadata: \r\n\r\n" + ex);
                }
                #endregion


                #region Prepare attributes - 45%

                //Prepare Attributes
                subProcess.Reset();
                subProcess.Start();

                _alert.SetTextLogging("\r\n");

                var attCounter = 1;

                // Dummy row - insert 'Not Applicable' attribute to satisfy RI
                using (var connection = new SqlConnection(metaDataConnection))
                {
                    var insertNAStatement = new StringBuilder();

                    insertNAStatement.AppendLine("INSERT INTO [MD_ATTRIBUTE]");
                    insertNAStatement.AppendLine("([ATTRIBUTE_NAME])");
                    insertNAStatement.AppendLine("VALUES ('Not applicable')");

                    var commandNA = new SqlCommand(insertNAStatement.ToString(), connection);

                    var insertNULLStatement = new StringBuilder();

                    insertNULLStatement.AppendLine("INSERT INTO [MD_ATTRIBUTE]");
                    insertNULLStatement.AppendLine("([ATTRIBUTE_NAME])");
                    insertNULLStatement.AppendLine("VALUES ('NULL')");

                    var commandNULL = new SqlCommand(insertNULLStatement.ToString(), connection);

                    try
                    {
                        connection.Open();
                        commandNA.ExecuteNonQuery();
                        attCounter++;
                        commandNULL.ExecuteNonQuery();
                        attCounter++;
                    }
                    catch (Exception ex)
                    {
                        errorCounter++;
                        _alert.SetTextLogging("An issue has occured during preparation of the attribute metadata. Please check the Error Log for more details.\r\n");

                        errorLog.AppendLine("\r\nAn issue has occured during preparation of attribute metadata: \r\n\r\n" + ex);
                        errorLog.AppendLine("\r\nThe query that caused the issue is: \r\n\r\n" + insertNAStatement);
                    }
                }

                /* Regular processing
                    RV: there is an issue below where not all SQL version (i.e. SQL Server) are supporting cross database SQL.
                    i.e. Azure. long term fix is to create individual queries to database without cross-db sql and add to single data table in the application
                */
                if (radioButtonPhysicalMode.Checked) // Read from live database
                {
                    _alert.SetTextLogging("Commencing preparing the attributes directly from the database.\r\n");
                }
                else // Virtual processing
                {
                    _alert.SetTextLogging("Commencing preparing the attributes from the metadata.\r\n");
                }

                var prepareAttStatement = new StringBuilder();
                prepareAttStatement.AppendLine("SELECT DISTINCT(COLUMN_NAME) AS COLUMN_NAME FROM (");

                prepareAttStatement.AppendLine("SELECT");
                prepareAttStatement.AppendLine("  [DATABASE_NAME]");
                prepareAttStatement.AppendLine(" ,[SCHEMA_NAME]");
                prepareAttStatement.AppendLine(" ,[TABLE_NAME]");
                prepareAttStatement.AppendLine(" ,[COLUMN_NAME]");
                prepareAttStatement.AppendLine(" ,[DATA_TYPE]");
                prepareAttStatement.AppendLine(" ,[CHARACTER_MAXIMUM_LENGTH]");
                prepareAttStatement.AppendLine(" ,[NUMERIC_PRECISION]");
                prepareAttStatement.AppendLine(" ,[ORDINAL_POSITION]");
                prepareAttStatement.AppendLine(" ,[PRIMARY_KEY_INDICATOR]");
                prepareAttStatement.AppendLine("FROM [MD_PHYSICAL_MODEL]");

                prepareAttStatement.AppendLine(") sub");
                prepareAttStatement.AppendLine("WHERE sub.COLUMN_NAME NOT IN");
                prepareAttStatement.AppendLine("  ( ");
                prepareAttStatement.AppendLine("    '" + recordSource + "',");
                prepareAttStatement.AppendLine("    '" + alternativeRecordSource + "',");
                prepareAttStatement.AppendLine("    '" + sourceRowId + "',");
                prepareAttStatement.AppendLine("    '" + recordChecksum + "',");
                prepareAttStatement.AppendLine("    '" + changeDataCaptureIndicator + "',");
                prepareAttStatement.AppendLine("    '" + hubAlternativeLdts + "',");
                prepareAttStatement.AppendLine("    '" + eventDateTimeAtttribute + "',");
                prepareAttStatement.AppendLine("    '" + effectiveDateTimeAttribute + "',");
                prepareAttStatement.AppendLine("    '" + etlProcessId + "',");
                prepareAttStatement.AppendLine("    '" + loadDateTimeStamp + "',");
                prepareAttStatement.AppendLine("    '" + currentRecordAttribute + "'");
                prepareAttStatement.AppendLine("  ) ");

                // Load the data table, get the attributes
                var listAtt = Utility.GetDataTable(ref connOmd, prepareAttStatement.ToString());

                // Check if there are any attributes found, otherwise insert into the repository
                if (listAtt.Rows.Count == 0)
                {
                    _alert.SetTextLogging("--> No attributes were found in the metadata, did you reverse-engineer the model?\r\n");
                }
                else
                {
                    foreach (DataRow tableName in listAtt.Rows)
                    {
                        using (var connection = new SqlConnection(metaDataConnection))
                        {
                            //_alert.SetTextLogging("--> Processing " + tableName["COLUMN_NAME"] + ".\r\n");

                            var insertStatement = new StringBuilder();

                            insertStatement.AppendLine("INSERT INTO [MD_ATTRIBUTE]");
                            insertStatement.AppendLine("([ATTRIBUTE_NAME])");
                            insertStatement.AppendLine("VALUES ('" + tableName["COLUMN_NAME"].ToString().Trim() + "')");

                            var command = new SqlCommand(insertStatement.ToString(), connection);

                            try
                            {
                                connection.Open();
                                command.ExecuteNonQuery();
                                attCounter++;
                            }
                            catch (Exception ex)
                            {
                                errorCounter++;
                                _alert.SetTextLogging("An issue has occured during preparation of the attribute metadata. Please check the Error Log for more details.\r\n");
                                errorLog.AppendLine("\r\nAn issue has occured during preparation of attribute metadata: \r\n\r\n" + ex);
                                errorLog.AppendLine("\r\nThe query that caused the issue is: \r\n\r\n" + insertStatement);
                            }
                        }
                    }

                    _alert.SetTextLogging("--> Processing " + attCounter + " attributes.\r\n");
                }

                worker.ReportProgress(45);
                subProcess.Stop();
                _alert.SetTextLogging("Preparation of the attributes completed, and has taken " + subProcess.Elapsed.TotalSeconds + " seconds.\r\n");
                #endregion


                #region Business Key - 50%

                //Understanding the Business Key (MD_BUSINESS_KEY_COMPONENT)
                subProcess.Reset();
                subProcess.Start();

                _alert.SetTextLogging("\r\n");
                _alert.SetTextLogging("Commencing the definition of the Business Key.\r\n");

                var prepareKeyStatement = new StringBuilder();

                prepareKeyStatement.AppendLine("SELECT");
                prepareKeyStatement.AppendLine("  SOURCE_NAME,");
                prepareKeyStatement.AppendLine("  TARGET_NAME,");
                prepareKeyStatement.AppendLine("  BUSINESS_KEY_ATTRIBUTE,");
                prepareKeyStatement.AppendLine(
                    "  ROW_NUMBER() OVER(PARTITION BY SOURCE_NAME, TARGET_NAME, BUSINESS_KEY_ATTRIBUTE ORDER BY SOURCE_NAME, TARGET_NAME, COMPONENT_ORDER ASC) AS COMPONENT_ID,");
                prepareKeyStatement.AppendLine("  COMPONENT_ORDER,");
                prepareKeyStatement.AppendLine("  REPLACE(COMPONENT_VALUE,'COMPOSITE(', '') AS COMPONENT_VALUE,");
                prepareKeyStatement.AppendLine("    CASE");
                prepareKeyStatement.AppendLine(
                    "            WHEN SUBSTRING(BUSINESS_KEY_ATTRIBUTE,1, 11)= 'CONCATENATE' THEN 'CONCATENATE()'");
                prepareKeyStatement.AppendLine(
                    "            WHEN SUBSTRING(BUSINESS_KEY_ATTRIBUTE,1, 6)= 'PIVOT' THEN 'PIVOT()'");
                prepareKeyStatement.AppendLine(
                    "            WHEN SUBSTRING(BUSINESS_KEY_ATTRIBUTE,1, 9)= 'COMPOSITE' THEN 'COMPOSITE()'");
                prepareKeyStatement.AppendLine("            ELSE 'NORMAL'");
                prepareKeyStatement.AppendLine("    END AS COMPONENT_TYPE");
                prepareKeyStatement.AppendLine("FROM");
                prepareKeyStatement.AppendLine("(");
                prepareKeyStatement.AppendLine("    SELECT DISTINCT");
                prepareKeyStatement.AppendLine("        A.SOURCE_TABLE,");
                prepareKeyStatement.AppendLine("        A.BUSINESS_KEY_ATTRIBUTE,");
                prepareKeyStatement.AppendLine("        A.TARGET_TABLE,");
                prepareKeyStatement.AppendLine("        CASE");
                prepareKeyStatement.AppendLine(
                    "            WHEN CHARINDEX('(', RTRIM(LTRIM(Split.a.value('.', 'VARCHAR(MAX)')))) > 0");
                prepareKeyStatement.AppendLine("            THEN RTRIM(LTRIM(Split.a.value('.', 'VARCHAR(MAX)')))");
                prepareKeyStatement.AppendLine(
                    "            ELSE REPLACE(RTRIM(LTRIM(Split.a.value('.', 'VARCHAR(MAX)'))), ')', '')");
                prepareKeyStatement.AppendLine("        END AS COMPONENT_VALUE,");
                prepareKeyStatement.AppendLine(
                    "        ROW_NUMBER() OVER(PARTITION BY SOURCE_TABLE, TARGET_TABLE, BUSINESS_KEY_ATTRIBUTE ORDER BY SOURCE_TABLE, TARGET_TABLE, BUSINESS_KEY_ATTRIBUTE ASC) AS COMPONENT_ORDER");
                prepareKeyStatement.AppendLine("    FROM");
                prepareKeyStatement.AppendLine("    (");
                prepareKeyStatement.AppendLine("      SELECT");
                prepareKeyStatement.AppendLine("          SOURCE_TABLE, ");
                prepareKeyStatement.AppendLine("          TARGET_TABLE, ");
                prepareKeyStatement.AppendLine("          BUSINESS_KEY_ATTRIBUTE,");
                prepareKeyStatement.AppendLine(
                    "          CASE SUBSTRING(BUSINESS_KEY_ATTRIBUTE, 0, CHARINDEX('(', BUSINESS_KEY_ATTRIBUTE))");
                prepareKeyStatement.AppendLine(
                    "             WHEN 'COMPOSITE' THEN CONVERT(XML, '<M>' + REPLACE(BUSINESS_KEY_ATTRIBUTE, ';', '</M><M>') + '</M>') ");
                prepareKeyStatement.AppendLine(
                    "             ELSE CONVERT(XML, '<M>' + REPLACE(BUSINESS_KEY_ATTRIBUTE, ',', '</M><M>') + '</M>') ");
                prepareKeyStatement.AppendLine("          END AS BUSINESS_KEY_ATTRIBUTE_XML");
                prepareKeyStatement.AppendLine("        FROM");
                prepareKeyStatement.AppendLine("        (");
                prepareKeyStatement.AppendLine(
                    "            SELECT DISTINCT SOURCE_TABLE, TARGET_TABLE, LTRIM(RTRIM(BUSINESS_KEY_ATTRIBUTE)) AS BUSINESS_KEY_ATTRIBUTE");
                prepareKeyStatement.AppendLine("            FROM TMP_MD_TABLE_MAPPING");
                prepareKeyStatement.AppendLine("            WHERE TARGET_TABLE_TYPE = '" +
                                               MetadataHandling.TableTypes.CoreBusinessConcept + "'");
                prepareKeyStatement.AppendLine("              AND [ENABLED_INDICATOR] = 'True'");
                prepareKeyStatement.AppendLine("        ) TableName");
                prepareKeyStatement.AppendLine(
                    "    ) AS A CROSS APPLY BUSINESS_KEY_ATTRIBUTE_XML.nodes('/M') AS Split(a)");
                prepareKeyStatement.AppendLine(
                    "    WHERE BUSINESS_KEY_ATTRIBUTE <> 'N/A' AND A.BUSINESS_KEY_ATTRIBUTE != ''");
                prepareKeyStatement.AppendLine(") pivotsub");
                prepareKeyStatement.AppendLine("LEFT OUTER JOIN");
                prepareKeyStatement.AppendLine("       (");
                prepareKeyStatement.AppendLine("              SELECT SOURCE_NAME, [SCHEMA_NAME]");
                prepareKeyStatement.AppendLine("              FROM MD_SOURCE");
                prepareKeyStatement.AppendLine("       ) stgsub");
                prepareKeyStatement.AppendLine(
                    "ON pivotsub.SOURCE_TABLE = stgsub.[SCHEMA_NAME]+'.'+stgsub.SOURCE_NAME");
                prepareKeyStatement.AppendLine("LEFT OUTER JOIN");
                prepareKeyStatement.AppendLine("       (");
                prepareKeyStatement.AppendLine("              SELECT HUB_NAME AS TARGET_NAME, [SCHEMA_NAME]");
                prepareKeyStatement.AppendLine("              FROM MD_HUB");
                prepareKeyStatement.AppendLine("       ) hubsub");
                prepareKeyStatement.AppendLine(
                    "ON pivotsub.TARGET_TABLE = hubsub.[SCHEMA_NAME]+'.'+hubsub.TARGET_NAME");
                prepareKeyStatement.AppendLine("ORDER BY stgsub.SOURCE_NAME, hubsub.TARGET_NAME, COMPONENT_ORDER");

                var listKeys = Utility.GetDataTable(ref connOmd, prepareKeyStatement.ToString());

                if (listKeys.Rows.Count == 0)
                {
                    _alert.SetTextLogging(
                        "-- >  No attributes were found in the metadata, did you reverse-engineer the model?\r\n");
                }
                else
                {
                    foreach (DataRow tableName in listKeys.Rows)
                    {
                        using (var connection = new SqlConnection(metaDataConnection))
                        {

                            var keyComponent = tableName["COMPONENT_VALUE"]; //Handle quotes between SQL and C%
                            keyComponent = keyComponent.ToString().Replace("'", "''");

                            _alert.SetTextLogging("--> Processing the Business Key " +
                                                  tableName["BUSINESS_KEY_ATTRIBUTE"] + " (for component " +
                                                  keyComponent + ") from " + tableName["SOURCE_NAME"] + " to " +
                                                  tableName["TARGET_NAME"] + "\r\n");

                            var businessKeyDefinition = tableName["BUSINESS_KEY_ATTRIBUTE"].ToString().Trim();
                            businessKeyDefinition = businessKeyDefinition.Replace("'", "''");


                            var insertStatement = new StringBuilder();
                            insertStatement.AppendLine("INSERT INTO [MD_BUSINESS_KEY_COMPONENT]");
                            insertStatement.AppendLine(
                                "(SOURCE_NAME, HUB_NAME, BUSINESS_KEY_DEFINITION, COMPONENT_ID, COMPONENT_ORDER, COMPONENT_VALUE, COMPONENT_TYPE)");
                            insertStatement.AppendLine("VALUES ('" + tableName["SOURCE_NAME"] + "','" +
                                                       tableName["TARGET_NAME"] + "','" + businessKeyDefinition +
                                                       "','" + tableName["COMPONENT_ID"] + "','" +
                                                       tableName["COMPONENT_ORDER"] + "','" + keyComponent + "','" +
                                                       tableName["COMPONENT_TYPE"] + "')");

                            var command = new SqlCommand(insertStatement.ToString(), connection);

                            try
                            {
                                connection.Open();
                                command.ExecuteNonQuery();
                            }
                            catch (Exception ex)
                            {
                                errorCounter++;
                                _alert.SetTextLogging(
                                    "An issue has occured during preparation of the Business Key metadata. Please check the Error Log for more details.\r\n");

                                errorLog.AppendLine(
                                    "\r\nAn issue has occured during preparation of Business Key metadata: \r\n\r\n" +
                                    ex);
                                errorLog.AppendLine(
                                    "\r\nThe query that caused the issue is: \r\n\r\n" + insertStatement);
                            }
                        }
                    }
                }

                worker.ReportProgress(50);
                subProcess.Stop();
                _alert.SetTextLogging("Preparation of the Business Key definition completed, and has taken " +
                                      subProcess.Elapsed.TotalSeconds + " seconds.\r\n");

                #endregion


                #region Business Key components - 60%

                //Understanding the Business Key component parts
                subProcess.Reset();
                subProcess.Start();

                _alert.SetTextLogging("\r\n");
                _alert.SetTextLogging("Commencing the Business Key component analysis.\r\n");

                var prepareKeyComponentStatement = new StringBuilder();
                var keyPartCounter = 1;
                /*LBM 2019/01/10: Changing to use @ String*/
                prepareKeyComponentStatement.AppendLine(@"
                                                            SELECT DISTINCT
                                                              SOURCE_NAME,
                                                              HUB_NAME,
                                                              BUSINESS_KEY_DEFINITION,
                                                              COMPONENT_ID,
                                                              ROW_NUMBER() over(partition by SOURCE_NAME, HUB_NAME, BUSINESS_KEY_DEFINITION, COMPONENT_ID order by nullif(0 * Split.a.value('count(.)', 'int'), 0)) AS COMPONENT_ELEMENT_ID,
                                                              ROW_NUMBER() over(partition by SOURCE_NAME, HUB_NAME, BUSINESS_KEY_DEFINITION, COMPONENT_ID order by nullif(0 * Split.a.value('count(.)', 'int'), 0)) AS COMPONENT_ELEMENT_ORDER,
                                                              REPLACE(REPLACE(REPLACE(RTRIM(LTRIM(Split.a.value('.', 'VARCHAR(MAX)'))), 'CONCATENATE(', ''), ')', ''), 'COMPOSITE(', '') AS COMPONENT_ELEMENT_VALUE,
                                                              CASE
                                                                 WHEN charindex(CHAR(39), REPLACE(REPLACE(RTRIM(LTRIM(Split.a.value('.', 'VARCHAR(MAX)'))), 'CONCATENATE(', ''), ')', '')) = 1 THEN 'User Defined Value'
                                                                ELSE 'Attribute'
                                                              END AS COMPONENT_ELEMENT_TYPE,
                                                              COALESCE(att.ATTRIBUTE_NAME, 'Not applicable') AS ATTRIBUTE_NAME
                                                            FROM
                                                            (
                                                                SELECT
                                                                    SOURCE_NAME,
                                                                    HUB_NAME,
                                                                    BUSINESS_KEY_DEFINITION,
                                                                    COMPONENT_ID,
                                                                    COMPONENT_VALUE,
                                                                    CONVERT(XML, '<M>' + REPLACE(COMPONENT_VALUE, ';', '</M><M>') + '</M>') AS COMPONENT_VALUE_XML
														        FROM MD_BUSINESS_KEY_COMPONENT
                                                            ) AS A CROSS APPLY COMPONENT_VALUE_XML.nodes('/M') AS Split(a)
                                                            LEFT OUTER JOIN MD_ATTRIBUTE att ON
                                                                REPLACE(REPLACE(RTRIM(LTRIM(Split.a.value('.', 'VARCHAR(MAX)'))), 'CONCATENATE(', ''), ')', '') = att.ATTRIBUTE_NAME
                                                            WHERE COMPONENT_VALUE <> 'N/A' AND A.COMPONENT_VALUE != ''
                                                            ORDER BY A.SOURCE_NAME, A.HUB_NAME, BUSINESS_KEY_DEFINITION, A.COMPONENT_ID, COMPONENT_ELEMENT_ORDER
                                                        ");
                var listKeyParts = Utility.GetDataTable(ref connOmd, prepareKeyComponentStatement.ToString());

                if (listKeyParts.Rows.Count == 0)
                {
                    _alert.SetTextLogging(
                        "--> No attributes were found in the metadata, did you reverse-engineer the model?\r\n");
                }
                else
                {
                    foreach (DataRow tableName in listKeyParts.Rows)
                    {
                        using (var connection = new SqlConnection(metaDataConnection))
                        {


                            var keyComponent = tableName["COMPONENT_ELEMENT_VALUE"]; //Handle quotes between SQL and C#
                            keyComponent = keyComponent.ToString().Trim().Replace("'", "''");

                            var businessKeyDefinition = tableName["BUSINESS_KEY_DEFINITION"];
                            businessKeyDefinition = businessKeyDefinition.ToString().Trim().Replace("'", "''");

                            var insertStatement = new StringBuilder();
                            insertStatement.AppendLine("INSERT INTO [MD_BUSINESS_KEY_COMPONENT_PART]");
                            insertStatement.AppendLine(
                                "(SOURCE_NAME, HUB_NAME, BUSINESS_KEY_DEFINITION, COMPONENT_ID,COMPONENT_ELEMENT_ID,COMPONENT_ELEMENT_ORDER,COMPONENT_ELEMENT_VALUE,COMPONENT_ELEMENT_TYPE,ATTRIBUTE_NAME)");
                            insertStatement.AppendLine("VALUES ('" + tableName["SOURCE_NAME"] + "','" +
                                                       tableName["HUB_NAME"] + "','" + businessKeyDefinition + "','" +
                                                       tableName["COMPONENT_ID"] + "','" +
                                                       tableName["COMPONENT_ELEMENT_ID"] + "','" +
                                                       tableName["COMPONENT_ELEMENT_ORDER"] + "','" + keyComponent +
                                                       "','" + tableName["COMPONENT_ELEMENT_TYPE"] + "','" +
                                                       tableName["ATTRIBUTE_NAME"] + "')");

                            var command = new SqlCommand(insertStatement.ToString(), connection);

                            try
                            {
                                connection.Open();
                                command.ExecuteNonQuery();
                                keyPartCounter++;
                            }
                            catch (Exception ex)
                            {
                                errorCounter++;
                                _alert.SetTextLogging(
                                    "An issue has occured during preparation of the Business Key component metadata. Please check the Error Log for more details.\r\n");

                                errorLog.AppendLine(
                                    "\r\nAn issue has occured during preparation of Business Key component metadata: \r\n\r\n" +
                                    ex);
                                errorLog.AppendLine(
                                    "\r\nThe query that caused the issue is: \r\n\r\n" + insertStatement);
                            }
                        }
                    }
                }

                worker.ReportProgress(60);
                subProcess.Stop();
                _alert.SetTextLogging("--> Processing " + keyPartCounter + " Business Key component attributes.\r\n");
                _alert.SetTextLogging("Preparation of the Business Key components completed, and has taken " +
                                      subProcess.Elapsed.TotalSeconds + " seconds.\r\n");


                #endregion


                #region Hub / Link relationship - 75%
                //Prepare HUB / LNK xref
                subProcess.Reset();
                subProcess.Start();

                _alert.SetTextLogging("\r\n");
                _alert.SetTextLogging("Commencing preparing the relationship between Hubs and Links.\r\n");

                var virtualisationSnippet = new StringBuilder();
                if (radioButtonPhysicalMode.Checked)
                {
                    //virtualisationSnippet.AppendLine("SELECT ");
                    //virtualisationSnippet.AppendLine("  OBJECT_SCHEMA_NAME(OBJECT_ID, DB_ID('" + TeamConfigurationSettings.IntegrationDatabaseName + "')) AS LINK_SCHEMA,");
                    //virtualisationSnippet.AppendLine("  OBJECT_NAME(OBJECT_ID,DB_ID('" + TeamConfigurationSettings.IntegrationDatabaseName + "'))  AS LINK_NAME,");
                    //virtualisationSnippet.AppendLine("  [name] AS HUB_TARGET_KEY_NAME_IN_LINK,");
                    //virtualisationSnippet.AppendLine("  ROW_NUMBER() OVER(PARTITION BY OBJECT_NAME(OBJECT_ID,DB_ID('" + TeamConfigurationSettings.IntegrationDatabaseName + "')) ORDER BY column_id) AS LINK_ORDER");
                    //virtualisationSnippet.AppendLine("FROM " + linkedServer + integrationDatabase + @".sys.columns");
                    //virtualisationSnippet.AppendLine("WHERE [column_id] > 1");
                    //virtualisationSnippet.AppendLine("AND OBJECT_NAME(OBJECT_ID,DB_ID('" + TeamConfigurationSettings.IntegrationDatabaseName + "')) LIKE '" + lnkTablePrefix + @"'");
                    //virtualisationSnippet.AppendLine("   AND [name] NOT IN ('" +
                    //                                 TeamConfigurationSettings.RecordSourceAttribute + "','" +
                    //                                 TeamConfigurationSettings.AlternativeRecordSourceAttribute + "','" +
                    //                                 TeamConfigurationSettings.AlternativeLoadDateTimeAttribute + "','" +
                    //                                 TeamConfigurationSettings.AlternativeSatelliteLoadDateTimeAttribute + "','" +
                    //                                 TeamConfigurationSettings.EtlProcessAttribute + "','" +
                    //                                 TeamConfigurationSettings.LoadDateTimeAttribute + "" +
                    //                                 "')");

                    // Use the physical model snapshot.
                    virtualisationSnippet.AppendLine("SELECT");
                    virtualisationSnippet.AppendLine("  [SCHEMA_NAME] AS LINK_SCHEMA,");
                    virtualisationSnippet.AppendLine("  [TABLE_NAME]  AS LINK_NAME,");
                    virtualisationSnippet.AppendLine("  [COLUMN_NAME] AS HUB_TARGET_KEY_NAME_IN_LINK,");
                    virtualisationSnippet.AppendLine("  ROW_NUMBER() OVER(PARTITION BY [TABLE_NAME] ORDER BY ORDINAL_POSITION) AS LINK_ORDER");
                    virtualisationSnippet.AppendLine("FROM MD_PHYSICAL_MODEL");
                    virtualisationSnippet.AppendLine("WHERE [ORDINAL_POSITION] > 1");
                    virtualisationSnippet.AppendLine(" AND TABLE_NAME LIKE '" + lnkTablePrefix + @"'");
                    virtualisationSnippet.AppendLine(" AND COLUMN_NAME NOT IN ('" +
                                                     TeamConfigurationSettings.RecordSourceAttribute + "','" +
                                                     TeamConfigurationSettings.AlternativeRecordSourceAttribute + "','" +
                                                     TeamConfigurationSettings.AlternativeLoadDateTimeAttribute + "','" +
                                                     TeamConfigurationSettings.AlternativeSatelliteLoadDateTimeAttribute + "','" +
                                                     TeamConfigurationSettings.EtlProcessAttribute + "','" +
                                                     TeamConfigurationSettings.LoadDateTimeAttribute +
                                                     "')");
                }
                else
                {
                    virtualisationSnippet.AppendLine("SELECT");
                    virtualisationSnippet.AppendLine("  [SCHEMA_NAME] AS LINK_SCHEMA,");
                    virtualisationSnippet.AppendLine("  [TABLE_NAME]  AS LINK_NAME,");
                    virtualisationSnippet.AppendLine("  [COLUMN_NAME] AS HUB_TARGET_KEY_NAME_IN_LINK,");
                    virtualisationSnippet.AppendLine("  ROW_NUMBER() OVER(PARTITION BY[TABLE_NAME] ORDER BY ORDINAL_POSITION) AS LINK_ORDER");
                    virtualisationSnippet.AppendLine("FROM TMP_MD_VERSION_ATTRIBUTE");
                    virtualisationSnippet.AppendLine("WHERE [ORDINAL_POSITION] > 1");
                    virtualisationSnippet.AppendLine(" AND TABLE_NAME LIKE '" + lnkTablePrefix + @"'");
                    virtualisationSnippet.AppendLine(" AND COLUMN_NAME NOT IN ('" +
                                                     TeamConfigurationSettings.RecordSourceAttribute + "','" +
                                                     TeamConfigurationSettings.AlternativeRecordSourceAttribute + "','" +
                                                     TeamConfigurationSettings.AlternativeLoadDateTimeAttribute + "','" +
                                                     TeamConfigurationSettings.AlternativeSatelliteLoadDateTimeAttribute + "','" + 
                                                     TeamConfigurationSettings.EtlProcessAttribute + "','" + 
                                                     TeamConfigurationSettings.LoadDateTimeAttribute +
                                                     "')");
                }

         

                var prepareHubLnkXrefStatement = new StringBuilder();

                prepareHubLnkXrefStatement.AppendLine("SELECT");
                prepareHubLnkXrefStatement.AppendLine("  hub_tbl.HUB_NAME,");
                prepareHubLnkXrefStatement.AppendLine("  lnk_tbl.LINK_NAME,");
                prepareHubLnkXrefStatement.AppendLine("  lnk_hubkey_order.HUB_KEY_ORDER AS HUB_ORDER,");
                prepareHubLnkXrefStatement.AppendLine("  lnk_target_model.HUB_TARGET_KEY_NAME_IN_LINK");
                prepareHubLnkXrefStatement.AppendLine("FROM");
                prepareHubLnkXrefStatement.AppendLine("-- This base query adds the Link and its Hubs and their order by pivoting on the full business key");
                prepareHubLnkXrefStatement.AppendLine("(");
                prepareHubLnkXrefStatement.AppendLine("  SELECT");
                prepareHubLnkXrefStatement.AppendLine("    TARGET_TABLE,");
                prepareHubLnkXrefStatement.AppendLine("    SOURCE_TABLE,");
                prepareHubLnkXrefStatement.AppendLine("    BUSINESS_KEY_ATTRIBUTE,");
                prepareHubLnkXrefStatement.AppendLine("    LTRIM(Split.a.value('.', 'VARCHAR(4000)')) AS BUSINESS_KEY_PART,");
                prepareHubLnkXrefStatement.AppendLine("    ROW_NUMBER() OVER(PARTITION BY TARGET_TABLE ORDER BY TARGET_TABLE) AS HUB_KEY_ORDER");
                prepareHubLnkXrefStatement.AppendLine("  FROM");
                prepareHubLnkXrefStatement.AppendLine("  (");
                prepareHubLnkXrefStatement.AppendLine("    SELECT");
                prepareHubLnkXrefStatement.AppendLine("      TARGET_TABLE,");
                prepareHubLnkXrefStatement.AppendLine("      SOURCE_TABLE,");
                prepareHubLnkXrefStatement.AppendLine("      ROW_NUMBER() OVER(PARTITION BY TARGET_TABLE ORDER BY TARGET_TABLE) AS LINK_ORDER,");
                prepareHubLnkXrefStatement.AppendLine("      BUSINESS_KEY_ATTRIBUTE, CAST('<M>' + REPLACE(BUSINESS_KEY_ATTRIBUTE, ',', '</M><M>') + '</M>' AS XML) AS BUSINESS_KEY_SOURCE_XML");
                prepareHubLnkXrefStatement.AppendLine("    FROM  TMP_MD_TABLE_MAPPING");
                prepareHubLnkXrefStatement.AppendLine("    WHERE [TARGET_TABLE_TYPE] = '" + MetadataHandling.TableTypes.NaturalBusinessRelationship + "'");
                prepareHubLnkXrefStatement.AppendLine("    AND [ENABLED_INDICATOR] = 'True'");
                prepareHubLnkXrefStatement.AppendLine("  ) AS A CROSS APPLY BUSINESS_KEY_SOURCE_XML.nodes('/M') AS Split(a)");
                prepareHubLnkXrefStatement.AppendLine("  WHERE LINK_ORDER=1 --Any link will do, the order of the Hub keys in the Link will always be the same");
                prepareHubLnkXrefStatement.AppendLine(") lnk_hubkey_order");
                prepareHubLnkXrefStatement.AppendLine("-- Adding the information required for the target model in the query");
                prepareHubLnkXrefStatement.AppendLine(" JOIN ");
                prepareHubLnkXrefStatement.AppendLine(" (");
                prepareHubLnkXrefStatement.AppendLine(virtualisationSnippet.ToString());
                prepareHubLnkXrefStatement.AppendLine(" ) lnk_target_model");
                prepareHubLnkXrefStatement.AppendLine(" ON lnk_hubkey_order.TARGET_TABLE = lnk_target_model.LINK_SCHEMA+'.'+lnk_target_model.LINK_NAME COLLATE DATABASE_DEFAULT");
                prepareHubLnkXrefStatement.AppendLine(" AND lnk_hubkey_order.HUB_KEY_ORDER = lnk_target_model.LINK_ORDER");
                prepareHubLnkXrefStatement.AppendLine(" --Adding the Hub mapping data to get the business keys");
                prepareHubLnkXrefStatement.AppendLine(" JOIN TMP_MD_TABLE_MAPPING hub");
                prepareHubLnkXrefStatement.AppendLine("     ON lnk_hubkey_order.[SOURCE_TABLE] = hub.SOURCE_TABLE");
                prepareHubLnkXrefStatement.AppendLine("     AND lnk_hubkey_order.[BUSINESS_KEY_PART] = hub.BUSINESS_KEY_ATTRIBUTE-- This condition is required to remove the redundant rows caused by the Link key pivoting");
                prepareHubLnkXrefStatement.AppendLine("     AND hub.[TARGET_TABLE_TYPE] = '" + MetadataHandling.TableTypes.CoreBusinessConcept + "'");
                prepareHubLnkXrefStatement.AppendLine("     AND hub.[ENABLED_INDICATOR] = 'True'");
                prepareHubLnkXrefStatement.AppendLine(" --Lastly adding the IDs for the Hubs and Links");
                prepareHubLnkXrefStatement.AppendLine(" JOIN dbo.MD_HUB hub_tbl");
                prepareHubLnkXrefStatement.AppendLine("     ON hub.TARGET_TABLE = hub_tbl.[SCHEMA_NAME]+'.'+hub_tbl.HUB_NAME");
                prepareHubLnkXrefStatement.AppendLine(" JOIN dbo.MD_LINK lnk_tbl");
                prepareHubLnkXrefStatement.AppendLine("     ON lnk_hubkey_order.TARGET_TABLE = lnk_tbl.[SCHEMA_NAME]+'.'+lnk_tbl.LINK_NAME");

                var listHlXref = Utility.GetDataTable(ref connOmd, prepareHubLnkXrefStatement.ToString());

                if (listHlXref != null)
                {
                    foreach (DataRow tableName in listHlXref.Rows)
                    {
                        using (var connection = new SqlConnection(metaDataConnection))
                        {
                            _alert.SetTextLogging("--> Processing the " + tableName["HUB_NAME"] + " to " +
                                                  tableName["LINK_NAME"] + " relationship.\r\n");

                            var insertStatement = new StringBuilder();
                            insertStatement.AppendLine("INSERT INTO [MD_HUB_LINK_XREF]");
                            insertStatement.AppendLine(
                                "([HUB_NAME], [LINK_NAME], [HUB_ORDER], [HUB_TARGET_KEY_NAME_IN_LINK])");
                            insertStatement.AppendLine("VALUES ('" + tableName["HUB_NAME"] + "','" +
                                                       tableName["LINK_NAME"] + "','" + tableName["HUB_ORDER"] + "','" +
                                                       tableName["HUB_TARGET_KEY_NAME_IN_LINK"] + "')");

                            var command = new SqlCommand(insertStatement.ToString(), connection);

                            try
                            {
                                connection.Open();
                                command.ExecuteNonQuery();
                            }
                            catch (Exception ex)
                            {
                                errorCounter++;
                                _alert.SetTextLogging(
                                    "An issue has occured during preparation of the Hub / Link XREF metadata. Please check the Error Log for more details.\r\n");

                                errorLog.AppendLine(
                                    "\r\nAn issue has occured during preparation of the Hub / Link XREF metadata: \r\n\r\n" +
                                    ex);
                                errorLog.AppendLine(
                                    "\r\nThe query that caused the issue is: \r\n\r\n" + insertStatement);
                            }
                        }
                    }
                }

                worker.ReportProgress(75);
                subProcess.Stop();
                _alert.SetTextLogging(
                    "Preparation of the relationship between Hubs and Links completed, and has taken " +
                    subProcess.Elapsed.TotalSeconds + " seconds.\r\n");

                #endregion


                #region Link Business Key - 78%

                // Prepare links business key backfill

                _alert.SetTextLogging("\r\n");
                _alert.SetTextLogging("Commencing preparing the Link Business key metadata.\r\n");

                // Getting the distinct list of tables to go into the MD_LINK table
                selectionRows =
                    inputTableMetadata.Select(
                        TableMappingMetadataColumns.Enabled.ToString() + " = 'true' AND " + TableMappingMetadataColumns.TargetTable.ToString() + " LIKE '%" + lnkTablePrefix + "%'");

                var distincLinksForBusinessKey = new List<string>();

                // Create a distinct list of sources from the data grid
                foreach (DataRow row in selectionRows)
                {
                    string target_table = row[TableMappingMetadataColumns.TargetTable.ToString()].ToString().Trim();
                    if (!distincLinksForBusinessKey.Contains(target_table))
                    {
                        distincLinksForBusinessKey.Add(target_table);
                    }
                }

                // Insert the rest of the rows
                foreach (var tableName in distincLinksForBusinessKey)
                {
                    var fullyQualifiedName = MetadataHandling.GetSchema(tableName).FirstOrDefault();

                    var businessKeyList = MetadataHandling.GetLinkTargetBusinessKeyList(fullyQualifiedName.Key, fullyQualifiedName.Value, versionId, TeamConfigurationSettings.MetadataConnection.CreateSqlServerConnectionString(false));
                    string businessKey = string.Join(",", businessKeyList);

                    var updateStatement = new StringBuilder();

                    updateStatement.AppendLine("UPDATE [MD_LINK]");
                    updateStatement.AppendLine("SET [BUSINESS_KEY] = '" + businessKey + "'");
                    updateStatement.AppendLine("WHERE [SCHEMA_NAME] =  '" + fullyQualifiedName.Key + "'");
                    updateStatement.AppendLine("AND [LINK_NAME] =  '" + fullyQualifiedName.Value + "'");

                    var connection = new SqlConnection(metaDataConnection);
                    var command = new SqlCommand(updateStatement.ToString(), connection);

                    try
                    {
                        connection.Open();
                        command.ExecuteNonQuery();
                    }
                    catch (Exception ex)
                    {
                        errorCounter++;
                        _alert.SetTextLogging(
                            "An issue has occured during preparation of the Link Business Key. Please check the Error Log for more details.\r\n");

                        errorLog.AppendLine(
                            "\r\nAn issue has occured during preparation of the Link Business Key: \r\n\r\n" + ex);
                        errorLog.AppendLine("\r\nThe query that caused the issue is: \r\n\r\n" + updateStatement);
                    }
                }

                #endregion


                #region Stg / Link relationship - 80%

                //Prepare STG / LNK xref
                subProcess.Reset();
                subProcess.Start();

                _alert.SetTextLogging("\r\n");
                _alert.SetTextLogging("Commencing preparing the relationship between Source and Link tables.\r\n");

                var preparestgLnkXrefStatement = new StringBuilder();
                preparestgLnkXrefStatement.AppendLine("SELECT");
                preparestgLnkXrefStatement.AppendLine("  lnk_tbl.LINK_NAME,");
                preparestgLnkXrefStatement.AppendLine("  stg_tbl.SOURCE_NAME,");
                preparestgLnkXrefStatement.AppendLine("  lnk.FILTER_CRITERIA,");
                preparestgLnkXrefStatement.AppendLine("  lnk.BUSINESS_KEY_ATTRIBUTE");
                preparestgLnkXrefStatement.AppendLine("FROM [dbo].[TMP_MD_TABLE_MAPPING] lnk");
                preparestgLnkXrefStatement.AppendLine(
                    "JOIN [dbo].[MD_LINK] lnk_tbl ON lnk.TARGET_TABLE = lnk_tbl.[SCHEMA_NAME]+'.'+lnk_tbl.LINK_NAME");
                preparestgLnkXrefStatement.AppendLine(
                    "JOIN [dbo].[MD_SOURCE] stg_tbl ON lnk.SOURCE_TABLE = stg_tbl.[SCHEMA_NAME]+'.'+stg_tbl.SOURCE_NAME");
                preparestgLnkXrefStatement.AppendLine("WHERE lnk.TARGET_TABLE_TYPE = '" +
                                                      MetadataHandling.TableTypes.NaturalBusinessRelationship +
                                                      "'");
                preparestgLnkXrefStatement.AppendLine("AND[ENABLED_INDICATOR] = 'True'");

                var listStgLinkXref = Utility.GetDataTable(ref connOmd, preparestgLnkXrefStatement.ToString());

                foreach (DataRow tableName in listStgLinkXref.Rows)
                {
                    using (var connection = new SqlConnection(metaDataConnection))
                    {
                        _alert.SetTextLogging("--> Processing the " + tableName["SOURCE_NAME"] + " to " +
                                              tableName["LINK_NAME"] + " relationship.\r\n");


                        var filterCriterion = tableName["FILTER_CRITERIA"].ToString().Trim();
                        filterCriterion = filterCriterion.Replace("'", "''");

                        var businessKeyDefinition = tableName["BUSINESS_KEY_ATTRIBUTE"].ToString().Trim();
                        businessKeyDefinition = businessKeyDefinition.Replace("'", "''");

                        var loadVector = MetadataHandling.GetLoadVector(tableName["SOURCE_NAME"].ToString(),
                            tableName["LINK_NAME"].ToString(), TeamConfigurationSettings);


                        var insertStatement = new StringBuilder();
                        insertStatement.AppendLine("INSERT INTO [MD_SOURCE_LINK_XREF]");
                        insertStatement.AppendLine(
                            "([SOURCE_NAME], [LINK_NAME], [FILTER_CRITERIA], [BUSINESS_KEY_DEFINITION], [LOAD_VECTOR])");
                        insertStatement.AppendLine("VALUES ('" + tableName["SOURCE_NAME"] +
                                                   "','" + tableName["LINK_NAME"] +
                                                   "','" + filterCriterion +
                                                   "','" + businessKeyDefinition +
                                                   "','" + loadVector +
                                                   "')");

                        var command = new SqlCommand(insertStatement.ToString(), connection);

                        try
                        {
                            connection.Open();
                            command.ExecuteNonQuery();
                        }
                        catch (Exception ex)
                        {
                            errorCounter++;
                            _alert.SetTextLogging(
                                "An issue has occured during preparation of the Hub / Link XREF metadata. Please check the Error Log for more details.\r\n");

                            errorLog.AppendLine(
                                "\r\nAn issue has occured during preparation of the Hub / Link XREF metadata: \r\n\r\n" +
                                ex);
                            errorLog.AppendLine("\r\nThe query that caused the issue is: \r\n\r\n" + insertStatement);
                        }
                    }
                }

                worker.ReportProgress(80);
                subProcess.Stop();
                _alert.SetTextLogging(
                    "Preparation of the relationship between Source and the Links completed, and has taken " +
                    subProcess.Elapsed.TotalSeconds + " seconds.\r\n");

                #endregion


                #region Manually mapped Source to Staging Area Attribute XREF - 81%

                // Prepare the Source to Staging Area XREF
                subProcess.Reset();
                subProcess.Start();

                _alert.SetTextLogging("\r\n");
                _alert.SetTextLogging(
                    "Commencing preparing the Source to Staging column-to-column mapping metadata based on the manual mappings.\r\n");

                // Getting the distinct list of row from the data table
                selectionRows = inputAttributeMetadata.Select("TARGET_TABLE LIKE '%" + stagingPrefix + "%'");

                if (selectionRows.Length == 0)
                {
                    _alert.SetTextLogging("No manual column-to-column mappings for Source-to-Staging were detected.\r\n");
                }
                else
                {
                    // Process the unique Staging Area records
                    foreach (var row in selectionRows)
                    {
                        if (localTableEnabledDictionary.TryGetValue(row["TARGET_TABLE"].ToString(),
                                out var enabledValue) == true)
                        {
                            // the key isn't in the dictionary.



                            using (var connection = new SqlConnection(metaDataConnection))
                            {
                                _alert.SetTextLogging("--> Processing the mapping from " + row["SOURCE_TABLE"] + " - " +
                                                      (string) row["SOURCE_COLUMN"] + " to " + row["TARGET_TABLE"] +
                                                      " - " +
                                                      (string) row["TARGET_COLUMN"] + ".\r\n");

                                var insertStatement = new StringBuilder();
                                insertStatement.AppendLine("INSERT INTO [MD_SOURCE_STAGING_ATTRIBUTE_XREF]");
                                insertStatement.AppendLine(
                                    "([SOURCE_NAME], [STAGING_NAME], [ATTRIBUTE_NAME_FROM], [ATTRIBUTE_NAME_TO], [MAPPING_TYPE])");
                                insertStatement.AppendLine("VALUES (" +
                                                           "'" + row["SOURCE_TABLE"] + "'," +
                                                           "'" + row["TARGET_TABLE"] + "', " +
                                                           "'" + (string) row["SOURCE_COLUMN"] + "', " +
                                                           "'" + (string) row["TARGET_COLUMN"] + "', " +
                                                           "'Manual mapping'" +
                                                           ")");

                                var command = new SqlCommand(insertStatement.ToString(), connection);

                                try
                                {
                                    connection.Open();
                                    command.ExecuteNonQuery();
                                }
                                catch (Exception ex)
                                {
                                    errorCounter++;
                                    _alert.SetTextLogging(
                                        "An issue has occured during preparation of the attribute mapping between the Source and the Staging Area. Please check the Error Log for more details.\r\n");

                                    errorLog.AppendLine(
                                        "\r\nAn issue has occured during preparation of the Source to Staging attribute mapping: \r\n\r\n" +
                                        ex);
                                    errorLog.AppendLine(
                                        "\r\nThe query that caused the issue is: \r\n\r\n" + insertStatement);
                                }
                            }
                        }
                        else
                        {
                            errorLog.AppendLine($"\r\nThe enabled / disabled state for {row["TARGET_TABLE"].ToString()} could not be asserted.\r\n\r\n");
                        }
                    }
                }

                worker?.ReportProgress(87);
                subProcess.Stop();
                _alert.SetTextLogging(
                    "Preparation of the manual column-to-column mappings for Source-to-Staging completed, and has taken " +
                    subProcess.Elapsed.TotalSeconds + " seconds.\r\n");

                #endregion


                #region Automatically mapped Source to Staging Area Attribute XREF 93%

                //Prepare automatic attribute mapping
                subProcess.Reset();
                subProcess.Start();

                _alert.SetTextLogging("\r\n");

                int automaticMappingCounter = 0;

                if (radioButtonPhysicalMode.Checked)
                {
                    _alert.SetTextLogging(
                        "Commencing preparing the (automatic) column-to-column mapping metadata for Source to Staging, based on what's available in the database.\r\n");
                }
                else
                {
                    _alert.SetTextLogging(
                        "Commencing preparing the (automatic) column-to-column mapping metadata for Source to Staging, based on what's available in the physical model metadata.\r\n");
                }

                // Run the statement, the virtual vs. physical lookups are embedded in MD_PHYSICAL_MODEL
                var prepareMappingStagingStatement = new StringBuilder();
                prepareMappingStagingStatement.AppendLine("WITH ALL_DATABASE_COLUMNS AS");
                prepareMappingStagingStatement.AppendLine("(");

                prepareMappingStagingStatement.AppendLine("SELECT");
                prepareMappingStagingStatement.AppendLine("  [DATABASE_NAME]");
                prepareMappingStagingStatement.AppendLine(" ,[SCHEMA_NAME]");
                prepareMappingStagingStatement.AppendLine(" ,[TABLE_NAME]");
                prepareMappingStagingStatement.AppendLine(" ,[COLUMN_NAME]");
                prepareMappingStagingStatement.AppendLine(" ,[DATA_TYPE]");
                prepareMappingStagingStatement.AppendLine(" ,[CHARACTER_MAXIMUM_LENGTH]");
                prepareMappingStagingStatement.AppendLine(" ,[NUMERIC_PRECISION]");
                prepareMappingStagingStatement.AppendLine(" ,[ORDINAL_POSITION]");
                prepareMappingStagingStatement.AppendLine(" ,[PRIMARY_KEY_INDICATOR]");
                prepareMappingStagingStatement.AppendLine("FROM [MD_PHYSICAL_MODEL]");

                prepareMappingStagingStatement.AppendLine("),");
                prepareMappingStagingStatement.AppendLine("XREF AS");
                prepareMappingStagingStatement.AppendLine("(");
                prepareMappingStagingStatement.AppendLine("  SELECT");
                prepareMappingStagingStatement.AppendLine("    xref.*,");
                prepareMappingStagingStatement.AppendLine("    src.[SCHEMA_NAME] AS SOURCE_SCHEMA_NAME,");
                prepareMappingStagingStatement.AppendLine("    tgt.[SCHEMA_NAME] AS TARGET_SCHEMA_NAME");
                prepareMappingStagingStatement.AppendLine("  FROM MD_SOURCE_STAGING_XREF xref");
                prepareMappingStagingStatement.AppendLine("LEFT OUTER JOIN dbo.MD_SOURCE src ON xref.SOURCE_NAME = src.SOURCE_NAME");
                prepareMappingStagingStatement.AppendLine("LEFT OUTER JOIN dbo.MD_STAGING tgt ON xref.STAGING_NAME = tgt.STAGING_NAME");
                prepareMappingStagingStatement.AppendLine(") ");
                prepareMappingStagingStatement.AppendLine("SELECT");
                prepareMappingStagingStatement.AppendLine("  XREF.SOURCE_NAME, ");
                prepareMappingStagingStatement.AppendLine("  XREF.STAGING_NAME,");
                prepareMappingStagingStatement.AppendLine("  ADC_TARGET.COLUMN_NAME AS ATTRIBUTE_NAME_FROM,");
                prepareMappingStagingStatement.AppendLine("  ADC_TARGET.COLUMN_NAME AS ATTRIBUTE_NAME_TO,");
                prepareMappingStagingStatement.AppendLine("  'automatically mapped' as VERIFICATION");
                prepareMappingStagingStatement.AppendLine("FROM XREF");
                prepareMappingStagingStatement.AppendLine("JOIN ALL_DATABASE_COLUMNS ADC_TARGET ON XREF.TARGET_SCHEMA_NAME = ADC_TARGET.[SCHEMA_NAME] AND XREF.STAGING_NAME = ADC_TARGET.TABLE_NAME");
                prepareMappingStagingStatement.AppendLine("JOIN dbo.MD_ATTRIBUTE tgt_attr ON ADC_TARGET.COLUMN_NAME = tgt_attr.ATTRIBUTE_NAME COLLATE DATABASE_DEFAULT");
                prepareMappingStagingStatement.AppendLine("WHERE NOT EXISTS (");
                prepareMappingStagingStatement.AppendLine("  SELECT SOURCE_NAME, STAGING_NAME, ATTRIBUTE_NAME_FROM");
                prepareMappingStagingStatement.AppendLine("  FROM MD_SOURCE_STAGING_ATTRIBUTE_XREF manualmapping");
                prepareMappingStagingStatement.AppendLine("WHERE");
                prepareMappingStagingStatement.AppendLine("      manualmapping.SOURCE_NAME = XREF.SOURCE_NAME");
                prepareMappingStagingStatement.AppendLine("  AND manualmapping.STAGING_NAME = XREF.STAGING_NAME");
                prepareMappingStagingStatement.AppendLine("  AND manualmapping.ATTRIBUTE_NAME_FROM = ADC_TARGET.COLUMN_NAME");
                prepareMappingStagingStatement.AppendLine(")");
                prepareMappingStagingStatement.AppendLine("ORDER BY SOURCE_NAME");


                var automaticAttributeMappings = Utility.GetDataTable(ref connOmd, prepareMappingStagingStatement.ToString());

                if (automaticAttributeMappings.Rows.Count == 0)
                {
                    _alert.SetTextLogging("--> No automatic column-to-column mappings were detected.\r\n");
                }
                else
                {
                    // Process the unique attribute mappings
                    foreach (DataRow row in automaticAttributeMappings.Rows)
                    {
                        using (var connection = new SqlConnection(metaDataConnection))
                        {
                            _alert.SetTextLogging("--> Processing the mapping from " + (string) row["SOURCE_NAME"] +
                                                  " - " + (string) row["ATTRIBUTE_NAME_FROM"] + " to " +
                                                  (string) row["STAGING_NAME"] + " - " +
                                                  (string) row["ATTRIBUTE_NAME_TO"] + ".\r\n");

                            var insertStatement = new StringBuilder();
                            insertStatement.AppendLine("INSERT INTO [MD_SOURCE_STAGING_ATTRIBUTE_XREF]");
                            insertStatement.AppendLine("([SOURCE_NAME], [STAGING_NAME], [ATTRIBUTE_NAME_FROM], [ATTRIBUTE_NAME_TO], [MAPPING_TYPE])");
                            insertStatement.AppendLine("VALUES (" +
                                                       "'" + (string) row["SOURCE_NAME"] + "', " +
                                                       "'" + (string) row["STAGING_NAME"] + "', " +
                                                       "'" + (string) row["ATTRIBUTE_NAME_FROM"] + "', " +
                                                       "'" + (string) row["ATTRIBUTE_NAME_TO"] + "', " +
                                                       "'Automatic mapping'" +
                                                       ")");

                            var command = new SqlCommand(insertStatement.ToString(), connection);

                            try
                            {
                                connection.Open();
                                command.ExecuteNonQuery();
                                automaticMappingCounter++;
                            }
                            catch (Exception ex)
                            {
                                errorCounter++;
                                _alert.SetTextLogging(
                                    "An issue has occured during preparation of the attribute mapping between the Source and the Staging Area. Please check the Error Log for more details.\r\n");

                                errorLog.AppendLine(
                                    "\r\nAn issue has occured during preparation of the Source to Staging attribute mapping: \r\n\r\n" +
                                    ex);
                                errorLog.AppendLine(
                                    "\r\nThe query that caused the issue is: \r\n\r\n" + insertStatement);
                            }
                        }
                    }
                }

                worker.ReportProgress(90);
                subProcess.Stop();
                _alert.SetTextLogging("--> Processing " + automaticMappingCounter +
                                      " automatically added attribute mappings.\r\n");
                _alert.SetTextLogging(
                    "Preparation of the automatically mapped column-to-column metadata completed, and has taken " +
                    subProcess.Elapsed.TotalSeconds + " seconds.\r\n");
                #endregion


                #region Manually mapped Source to Persistent Staging Area Attribute XREF - 81%

                // Prepare the Source to Persistent Staging Area XREF
                subProcess.Reset();
                subProcess.Start();

                _alert.SetTextLogging("\r\n");
                _alert.SetTextLogging("Commencing preparing the Source to Persistent Staging column-to-column mapping metadata based on the manual mappings.\r\n");

                // Getting the distinct list of row from the data table
                selectionRows = inputAttributeMetadata.Select("TARGET_TABLE LIKE '%" + psaPrefix + "%'");

                if (selectionRows.Length == 0)
                {
                    _alert.SetTextLogging("No manual column-to-column mappings for Source to Persistent Staging were detected.\r\n");
                }
                else
                {
                    // Process the unique Persistent Staging Area records
                    foreach (var row in selectionRows)
                    {
                        // Only process rows whose parent is enabled
                        if (localTableEnabledDictionary.TryGetValue(row["SOURCE_TABLE"].ToString(),
                                out var enabledValue) == true)
                        {


                            using (var connection = new SqlConnection(metaDataConnection))
                            {
                                _alert.SetTextLogging("--> Processing the mapping from " + row["SOURCE_TABLE"] + " - " +
                                                      (string) row["SOURCE_COLUMN"] + " to " + row["TARGET_TABLE"] +
                                                      " - " +
                                                      (string) row["TARGET_COLUMN"] + ".\r\n");

                                //var localTableName = MetadataHandling.GetNonQualifiedTableName(row[TableMetadataColumns.TargetTable.ToString()].ToString());

                                var insertStatement = new StringBuilder();
                                insertStatement.AppendLine("INSERT INTO [MD_SOURCE_PERSISTENT_STAGING_ATTRIBUTE_XREF]");
                                insertStatement.AppendLine(
                                    "([SOURCE_NAME], [PERSISTENT_STAGING_NAME], [ATTRIBUTE_NAME_FROM], [ATTRIBUTE_NAME_TO], [MAPPING_TYPE])");
                                insertStatement.AppendLine("VALUES (" +
                                                           "'" + row["SOURCE_TABLE"] + "', " +
                                                           "'" + row["TARGET_TABLE"] + "', " +
                                                           "'" + (string) row["SOURCE_COLUMN"] + "', " +
                                                           "'" + (string) row["TARGET_COLUMN"] + "', " +
                                                           "'Manual mapping'" +
                                                           ")");

                                var command = new SqlCommand(insertStatement.ToString(), connection);

                                try
                                {
                                    connection.Open();
                                    command.ExecuteNonQuery();
                                }
                                catch (Exception ex)
                                {
                                    errorCounter++;
                                    _alert.SetTextLogging(
                                        "An issue has occured during preparation of the attribute mapping between the Source and the Persistent Staging Area. Please check the Error Log for more details.\r\n");

                                    errorLog.AppendLine(
                                        "\r\nAn issue has occured during preparation of the Source to Persistent Staging attribute mapping: \r\n\r\n" +
                                        ex);
                                    errorLog.AppendLine(
                                        "\r\nThe query that caused the issue is: \r\n\r\n" + insertStatement);
                                }
                            }
                        }
                        else
                        {
                            errorLog.AppendLine($"\r\nThe enabled / disabled state for {row["TARGET_TABLE"].ToString()} could not be asserted.\r\n\r\n");
                        }
                    }
                }

                worker?.ReportProgress(87);
                subProcess.Stop();
                _alert.SetTextLogging("Preparation of the manual column-to-column mappings for Source-to-Staging completed, and has taken " + subProcess.Elapsed.TotalSeconds + " seconds.\r\n");

                #endregion


                #region Automatically mapped Source to Persistent Staging Area Attribute XREF 93%

                // Prepare automatic attribute mapping
                subProcess.Reset();
                subProcess.Start();

                _alert.SetTextLogging("\r\n");

                var prepareMappingPersistentStagingStatement = new StringBuilder();

                automaticMappingCounter = 0;

                _alert.SetTextLogging(
                    radioButtonPhysicalMode.Checked
                        ? "Commencing preparing the (automatic) column-to-column mapping metadata for Source to Persistent Staging, based on what's available in the database.\r\n"
                        : "Commencing preparing the (automatic) column-to-column mapping metadata for Source to Persistent Staging, based on what's available in the physical model metadata.\r\n");

                prepareMappingPersistentStagingStatement.AppendLine("WITH ALL_DATABASE_COLUMNS AS");
                prepareMappingPersistentStagingStatement.AppendLine("(");
                prepareMappingPersistentStagingStatement.AppendLine("SELECT");
                prepareMappingPersistentStagingStatement.AppendLine("  [DATABASE_NAME]");
                prepareMappingPersistentStagingStatement.AppendLine(" ,[SCHEMA_NAME]");
                prepareMappingPersistentStagingStatement.AppendLine(" ,[TABLE_NAME]");
                prepareMappingPersistentStagingStatement.AppendLine(" ,[COLUMN_NAME]");
                prepareMappingPersistentStagingStatement.AppendLine(" ,[DATA_TYPE]");
                prepareMappingPersistentStagingStatement.AppendLine(" ,[CHARACTER_MAXIMUM_LENGTH]");
                prepareMappingPersistentStagingStatement.AppendLine(" ,[NUMERIC_PRECISION]");
                prepareMappingPersistentStagingStatement.AppendLine(" ,[ORDINAL_POSITION]");
                prepareMappingPersistentStagingStatement.AppendLine(" ,[PRIMARY_KEY_INDICATOR]");
                prepareMappingPersistentStagingStatement.AppendLine("FROM [MD_PHYSICAL_MODEL]");
                prepareMappingPersistentStagingStatement.AppendLine("),");
                prepareMappingPersistentStagingStatement.AppendLine("XREF AS");
                prepareMappingPersistentStagingStatement.AppendLine("(");
                prepareMappingPersistentStagingStatement.AppendLine("  SELECT");
                prepareMappingPersistentStagingStatement.AppendLine("    xref.*,");
                prepareMappingPersistentStagingStatement.AppendLine("    tgt.PERSISTENT_STAGING_NAME_SHORT,");
                prepareMappingPersistentStagingStatement.AppendLine("    src.[SCHEMA_NAME] AS SOURCE_SCHEMA_NAME,");
                prepareMappingPersistentStagingStatement.AppendLine("    tgt.[SCHEMA_NAME] AS TARGET_SCHEMA_NAME");
                prepareMappingPersistentStagingStatement.AppendLine("  FROM MD_SOURCE_PERSISTENT_STAGING_XREF xref");
                prepareMappingPersistentStagingStatement.AppendLine("LEFT OUTER JOIN dbo.MD_SOURCE src ON xref.SOURCE_NAME = src.SOURCE_NAME");
                prepareMappingPersistentStagingStatement.AppendLine("LEFT OUTER JOIN dbo.MD_PERSISTENT_STAGING tgt ON xref.PERSISTENT_STAGING_NAME = tgt.PERSISTENT_STAGING_NAME");
                prepareMappingPersistentStagingStatement.AppendLine(") ");
                prepareMappingPersistentStagingStatement.AppendLine("SELECT");
                prepareMappingPersistentStagingStatement.AppendLine("  XREF.SOURCE_NAME, ");
                prepareMappingPersistentStagingStatement.AppendLine("  XREF.PERSISTENT_STAGING_NAME,");
                prepareMappingPersistentStagingStatement.AppendLine("  ADC_TARGET.COLUMN_NAME AS ATTRIBUTE_NAME_FROM,");
                prepareMappingPersistentStagingStatement.AppendLine("  ADC_TARGET.COLUMN_NAME AS ATTRIBUTE_NAME_TO,");
                prepareMappingPersistentStagingStatement.AppendLine("  'automatically mapped' as VERIFICATION");
                prepareMappingPersistentStagingStatement.AppendLine("FROM XREF");
                prepareMappingPersistentStagingStatement.AppendLine("JOIN ALL_DATABASE_COLUMNS ADC_TARGET ON XREF.TARGET_SCHEMA_NAME = ADC_TARGET.[SCHEMA_NAME] AND XREF.PERSISTENT_STAGING_NAME_SHORT = ADC_TARGET.TABLE_NAME");
                prepareMappingPersistentStagingStatement.AppendLine("JOIN dbo.MD_ATTRIBUTE tgt_attr ON ADC_TARGET.COLUMN_NAME = tgt_attr.ATTRIBUTE_NAME COLLATE DATABASE_DEFAULT");
                prepareMappingPersistentStagingStatement.AppendLine("WHERE NOT EXISTS (");
                prepareMappingPersistentStagingStatement.AppendLine("  SELECT SOURCE_NAME, PERSISTENT_STAGING_NAME, ATTRIBUTE_NAME_FROM");
                prepareMappingPersistentStagingStatement.AppendLine("  FROM MD_SOURCE_PERSISTENT_STAGING_ATTRIBUTE_XREF manualmapping");
                prepareMappingPersistentStagingStatement.AppendLine("  WHERE");
                prepareMappingPersistentStagingStatement.AppendLine("      manualmapping.SOURCE_NAME = XREF.SOURCE_NAME");
                prepareMappingPersistentStagingStatement.AppendLine("  AND manualmapping.PERSISTENT_STAGING_NAME = XREF.PERSISTENT_STAGING_NAME");
                prepareMappingPersistentStagingStatement.AppendLine("  AND manualmapping.ATTRIBUTE_NAME_TO = ADC_TARGET.COLUMN_NAME");
                prepareMappingPersistentStagingStatement.AppendLine("  AND manualmapping.MAPPING_TYPE = 'Manual mapping'");
                prepareMappingPersistentStagingStatement.AppendLine(")");
                prepareMappingPersistentStagingStatement.AppendLine("ORDER BY SOURCE_NAME");

                var automaticAttributeMappingsPsa = Utility.GetDataTable(ref connOmd, prepareMappingPersistentStagingStatement.ToString());

                if (automaticAttributeMappingsPsa.Rows.Count == 0)
                {
                    _alert.SetTextLogging("--> No automatic column-to-column mappings were detected.\r\n");
                }
                else
                {
                    // Process the unique attribute mappings
                    foreach (DataRow row in automaticAttributeMappingsPsa.Rows)
                    {
                        using (var connection = new SqlConnection(metaDataConnection))
                        {
                            _alert.SetTextLogging("--> Processing the mapping from " + (string) row["SOURCE_NAME"] +
                                                  " - " + (string) row["ATTRIBUTE_NAME_FROM"] + " to " +
                                                  (string) row["PERSISTENT_STAGING_NAME"] + " - " +
                                                  (string) row["ATTRIBUTE_NAME_TO"] + ".\r\n");

                            var insertStatement = new StringBuilder();
                            insertStatement.AppendLine("INSERT INTO [MD_SOURCE_PERSISTENT_STAGING_ATTRIBUTE_XREF]");
                            insertStatement.AppendLine("([SOURCE_NAME], [PERSISTENT_STAGING_NAME], [ATTRIBUTE_NAME_FROM], [ATTRIBUTE_NAME_TO], [MAPPING_TYPE])");
                            insertStatement.AppendLine("VALUES ("  +
                                                       "'" + (string) row["SOURCE_NAME"] + "'," +
                                                       "'" + (string) row["PERSISTENT_STAGING_NAME"] + "', " +
                                                       "'" + (string) row["ATTRIBUTE_NAME_FROM"] + "', " +
                                                       "'" + (string) row["ATTRIBUTE_NAME_TO"] + "', " +
                                                       "'Automatic mapping'" +
                                                       ")");

                            var command = new SqlCommand(insertStatement.ToString(), connection);

                            try
                            {
                                connection.Open();
                                command.ExecuteNonQuery();
                                automaticMappingCounter++;
                            }
                            catch (Exception ex)
                            {
                                errorCounter++;
                                _alert.SetTextLogging(
                                    "An issue has occured during preparation of the attribute mapping between the Source and the Persistent Staging Area. Please check the Error Log for more details.\r\n");

                                errorLog.AppendLine(
                                    "\r\nAn issue has occured during preparation of the Source to Persistent Staging attribute mapping: \r\n\r\n" +
                                    ex);
                                errorLog.AppendLine(
                                    "\r\nThe query that caused the issue is: \r\n\r\n" + insertStatement);
                            }
                        }
                    }
                }

                worker.ReportProgress(90);
                subProcess.Stop();
                _alert.SetTextLogging("--> Processing " + automaticMappingCounter +
                                      " automatically added attribute mappings.\r\n");
                _alert.SetTextLogging(
                    "Preparation of the automatically mapped column-to-column metadata completed, and has taken " +
                    subProcess.Elapsed.TotalSeconds + " seconds.\r\n");


                #endregion


                #region Manually mapped attributes for SAT and LSAT 90%

                //Prepare Manual Attribute mapping for Satellites and Link Satellites
                subProcess.Reset();
                subProcess.Start();

                _alert.SetTextLogging("\r\n");
                _alert.SetTextLogging(
                    "Commencing preparing the Satellite and Link-Satellite column-to-column mapping metadata based on the manual mappings.\r\n");

                int manualSatMappingCounter = 0;

                var prepareMappingStatementManual = new StringBuilder();
                prepareMappingStatementManual.AppendLine("SELECT");
                prepareMappingStatementManual.AppendLine("   stg.SOURCE_NAME");
                prepareMappingStatementManual.AppendLine("  ,sat.SATELLITE_NAME");
                prepareMappingStatementManual.AppendLine("  ,stg_attr.ATTRIBUTE_NAME AS ATTRIBUTE_NAME_FROM");
                prepareMappingStatementManual.AppendLine("  ,target_attr.ATTRIBUTE_NAME AS ATTRIBUTE_NAME_TO");
                prepareMappingStatementManual.AppendLine("  ,'N' as MULTI_ACTIVE_KEY_INDICATOR");
                prepareMappingStatementManual.AppendLine("  ,'manually_mapped' as VERIFICATION");
                prepareMappingStatementManual.AppendLine("FROM dbo.TMP_MD_ATTRIBUTE_MAPPING mapping");
                prepareMappingStatementManual.AppendLine("LEFT OUTER JOIN dbo.MD_SATELLITE sat on sat.[SCHEMA_NAME]+'.'+sat.SATELLITE_NAME=mapping.TARGET_TABLE");
                prepareMappingStatementManual.AppendLine("LEFT OUTER JOIN dbo.MD_ATTRIBUTE target_attr on mapping.TARGET_COLUMN = target_attr.ATTRIBUTE_NAME");
                prepareMappingStatementManual.AppendLine("LEFT OUTER JOIN dbo.MD_SOURCE stg on stg.[SCHEMA_NAME]+'.'+stg.SOURCE_NAME = mapping.SOURCE_TABLE");
                prepareMappingStatementManual.AppendLine("LEFT OUTER JOIN dbo.MD_ATTRIBUTE stg_attr on mapping.SOURCE_COLUMN = stg_attr.ATTRIBUTE_NAME");
                prepareMappingStatementManual.AppendLine("LEFT OUTER JOIN dbo.TMP_MD_TABLE_MAPPING table_mapping");
                prepareMappingStatementManual.AppendLine("    ON mapping.TARGET_TABLE = table_mapping.TARGET_TABLE");
                prepareMappingStatementManual.AppendLine("AND mapping.SOURCE_TABLE = table_mapping.SOURCE_TABLE");
                prepareMappingStatementManual.AppendLine("WHERE mapping.TARGET_TABLE_TYPE IN ('" +
                                                         MetadataHandling.TableTypes.Context + "', '" +
                                                         MetadataHandling.TableTypes
                                                             .NaturalBusinessRelationshipContext + "')");
                prepareMappingStatementManual.AppendLine("   AND table_mapping.[ENABLED_INDICATOR] = 'True' ");


                var attributeMappingsSatellites = Utility.GetDataTable(ref connOmd, prepareMappingStatementManual.ToString());

                if (attributeMappingsSatellites.Rows.Count == 0)
                {
                    _alert.SetTextLogging("--> No manual column-to-column mappings were detected.\r\n");
                }
                else
                {
                    foreach (DataRow row in attributeMappingsSatellites.Rows)
                    {
                        using (var connection = new SqlConnection(metaDataConnection))
                        {
                            var insertStatement = new StringBuilder();
                            insertStatement.AppendLine("INSERT INTO [MD_SOURCE_SATELLITE_ATTRIBUTE_XREF]");
                            insertStatement.AppendLine("( [SOURCE_NAME],[SATELLITE_NAME],[ATTRIBUTE_NAME_FROM],[ATTRIBUTE_NAME_TO],[MULTI_ACTIVE_KEY_INDICATOR], [MAPPING_TYPE])");
                            insertStatement.AppendLine("VALUES (" +
                                                       "'" + row["SOURCE_NAME"] + "', " +
                                                       "'" + row["SATELLITE_NAME"] + "', " +
                                                       "'" + row["ATTRIBUTE_NAME_FROM"] + "', " +
                                                       "'" + row["ATTRIBUTE_NAME_TO"] + "', " +
                                                       "'" + row["MULTI_ACTIVE_KEY_INDICATOR"] + "'," +
                                                       "'Manual mapping'" +
                                                       ")");

                            try
                            {

                                var command = new SqlCommand(insertStatement.ToString(), connection);
                                connection.Open();
                                command.ExecuteNonQuery();
                                _alert.SetTextLogging("--> Processing the mapping from " + (string) row["SOURCE_NAME"] +
                                                      " - " + (string) row["ATTRIBUTE_NAME_FROM"] + " to " +
                                                      (string) row["SATELLITE_NAME"] + " - " +
                                                      (string) row["ATTRIBUTE_NAME_TO"] + ".\r\n");

                                manualSatMappingCounter++;

                            }
                            catch (Exception ex)
                            {
                                _alert.SetTextLogging("-----> An issue has occurred mapping columns from table " +
                                                      row["SOURCE_NAME"] + " to " + row["SATELLITE_NAME"] +
                                                      ". Please check the Error Log for more details.\r\n");
                                errorCounter++;
                                errorLog.AppendLine(
                                    "\r\nAn issue has occured during preparation of the Source to Satellite attribute mapping: \r\n\r\n" +
                                    ex);
                                errorLog.AppendLine(
                                    "\r\nThe query that caused the issue is: \r\n\r\n" + insertStatement);

                                if (row["ATTRIBUTE_NAME_FROM"].ToString() == "")
                                {
                                    _alert.SetTextLogging("Both attributes are NULL.");
                                }
                            }
                        }
                    }
                }

                worker.ReportProgress(90);
                subProcess.Stop();
                _alert.SetTextLogging("--> Processing " + manualSatMappingCounter + " manual attribute mappings.\r\n");
                _alert.SetTextLogging("Preparation of the manual column-to-column mapping for Satellites and Link-Satellites completed, and has taken " +
                                      subProcess.Elapsed.TotalSeconds + " seconds.\r\n");

                #endregion


                #region Automatically mapped attributes for SAT and LSAT 93%

                //Prepare automatic attribute mapping
                subProcess.Reset();
                subProcess.Start();

                _alert.SetTextLogging("\r\n");

                var prepareMappingStatement = new StringBuilder();

                if (radioButtonPhysicalMode.Checked)
                {
                    _alert.SetTextLogging(
                        "Commencing preparing the (automatic) column-to-column mapping metadata for Satellites and Link-Satellites, based on what's available in the database.\r\n");
                }
                else
                {
                    _alert.SetTextLogging(
                        "Commencing preparing the (automatic) column-to-column mapping metadata for Satellites and Link-Satellites, based on what's available in the physical model metadata.\r\n");
                }

                // Run the statement, the virtual vs. physical lookups are embedded in allDatabaseAttributes
                prepareMappingStatement.AppendLine("WITH ALL_DATABASE_COLUMNS AS");
                prepareMappingStatement.AppendLine("(");

                prepareMappingStatement.AppendLine("SELECT");
                prepareMappingStatement.AppendLine("  [DATABASE_NAME]");
                prepareMappingStatement.AppendLine(" ,[SCHEMA_NAME]");
                prepareMappingStatement.AppendLine(" ,[TABLE_NAME]");
                prepareMappingStatement.AppendLine(" ,[COLUMN_NAME]");
                prepareMappingStatement.AppendLine(" ,[DATA_TYPE]");
                prepareMappingStatement.AppendLine(" ,[CHARACTER_MAXIMUM_LENGTH]");
                prepareMappingStatement.AppendLine(" ,[NUMERIC_PRECISION]");
                prepareMappingStatement.AppendLine(" ,[ORDINAL_POSITION]");
                prepareMappingStatement.AppendLine(" ,[PRIMARY_KEY_INDICATOR]");
                prepareMappingStatement.AppendLine("FROM [MD_PHYSICAL_MODEL]");

                prepareMappingStatement.AppendLine("),");
                prepareMappingStatement.AppendLine("XREF AS");
                prepareMappingStatement.AppendLine("(");
                prepareMappingStatement.AppendLine("  SELECT");
                prepareMappingStatement.AppendLine("    src.[SCHEMA_NAME] AS SOURCE_SCHEMA_NAME,");
                prepareMappingStatement.AppendLine("    src.[SOURCE_NAME] AS SOURCE_NAME,");
                prepareMappingStatement.AppendLine("    tgt.[SCHEMA_NAME] AS TARGET_SCHEMA_NAME,");
                prepareMappingStatement.AppendLine("    tgt.SATELLITE_NAME AS TARGET_NAME");
                prepareMappingStatement.AppendLine("  FROM MD_SOURCE_SATELLITE_XREF xref");
                prepareMappingStatement.AppendLine(
                    "LEFT OUTER JOIN dbo.MD_SOURCE src ON xref.SOURCE_NAME = src.SOURCE_NAME");
                prepareMappingStatement.AppendLine(
                    "LEFT OUTER JOIN dbo.MD_SATELLITE tgt ON xref.SATELLITE_NAME = tgt.SATELLITE_NAME");
                prepareMappingStatement.AppendLine(")");
                prepareMappingStatement.AppendLine("SELECT");
                prepareMappingStatement.AppendLine("  XREF.SOURCE_NAME, ");
                prepareMappingStatement.AppendLine("  XREF.TARGET_NAME AS SATELLITE_NAME,");
                prepareMappingStatement.AppendLine("  ADC_SOURCE.COLUMN_NAME AS ATTRIBUTE_NAME_FROM,");
                prepareMappingStatement.AppendLine("  ADC_TARGET.COLUMN_NAME AS ATTRIBUTE_NAME_TO,");
                prepareMappingStatement.AppendLine("  'N' AS MULTI_ACTIVE_KEY_INDICATOR,");
                prepareMappingStatement.AppendLine("  'automatically mapped' as VERIFICATION");
                prepareMappingStatement.AppendLine("FROM XREF");
                prepareMappingStatement.AppendLine(
                    "JOIN ALL_DATABASE_COLUMNS ADC_SOURCE ON XREF.SOURCE_SCHEMA_NAME = ADC_SOURCE.[SCHEMA_NAME] AND XREF.SOURCE_NAME = ADC_SOURCE.TABLE_NAME");
                prepareMappingStatement.AppendLine(
                    "JOIN ALL_DATABASE_COLUMNS ADC_TARGET ON XREF.TARGET_SCHEMA_NAME = ADC_TARGET.[SCHEMA_NAME] AND XREF.TARGET_NAME = ADC_TARGET.TABLE_NAME");
                prepareMappingStatement.AppendLine(
                    "JOIN dbo.MD_ATTRIBUTE stg_attr ON ADC_SOURCE.COLUMN_NAME = stg_attr.ATTRIBUTE_NAME COLLATE DATABASE_DEFAULT");
                prepareMappingStatement.AppendLine(
                    "JOIN dbo.MD_ATTRIBUTE tgt_attr ON ADC_TARGET.COLUMN_NAME = tgt_attr.ATTRIBUTE_NAME COLLATE DATABASE_DEFAULT");
                prepareMappingStatement.AppendLine(
                    "WHERE UPPER(stg_attr.ATTRIBUTE_NAME) = UPPER(tgt_attr.ATTRIBUTE_NAME)");
                prepareMappingStatement.AppendLine("AND NOT EXISTS (");
                prepareMappingStatement.AppendLine("  SELECT SOURCE_NAME, SATELLITE_NAME, ATTRIBUTE_NAME_FROM");
                prepareMappingStatement.AppendLine("  FROM MD_SOURCE_SATELLITE_ATTRIBUTE_XREF manualmapping");
                prepareMappingStatement.AppendLine("  WHERE");
                prepareMappingStatement.AppendLine("      manualmapping.SOURCE_NAME = XREF.SOURCE_NAME");
                prepareMappingStatement.AppendLine("  AND manualmapping.SATELLITE_NAME = XREF.TARGET_NAME");
                prepareMappingStatement.AppendLine("  AND manualmapping.ATTRIBUTE_NAME_FROM = ADC_SOURCE.COLUMN_NAME");
                prepareMappingStatement.AppendLine(")");
                prepareMappingStatement.AppendLine("ORDER BY SOURCE_NAME");

                var automaticAttributeMappingsSatellites = Utility.GetDataTable(ref connOmd, prepareMappingStatement.ToString());

                if (automaticAttributeMappingsSatellites.Rows.Count == 0)
                {
                    _alert.SetTextLogging("--> No automatic column-to-column mappings were detected.\r\n");
                }
                else
                {
                    foreach (DataRow row in automaticAttributeMappingsSatellites.Rows)
                    {
                        using (var connection = new SqlConnection(metaDataConnection))
                        {
                            _alert.SetTextLogging("--> Processing the mapping from " + (string) row["SOURCE_NAME"] +
                                                  " - " + (string) row["ATTRIBUTE_NAME_FROM"] + " to " +
                                                  (string) row["SATELLITE_NAME"] + " - " +
                                                  (string) row["ATTRIBUTE_NAME_TO"] + ".\r\n");

                            var insertStatement = new StringBuilder();
                            insertStatement.AppendLine("INSERT INTO [MD_SOURCE_SATELLITE_ATTRIBUTE_XREF]");
                            insertStatement.AppendLine("( [SOURCE_NAME],[SATELLITE_NAME],[ATTRIBUTE_NAME_FROM],[ATTRIBUTE_NAME_TO],[MULTI_ACTIVE_KEY_INDICATOR], [MAPPING_TYPE])");
                            insertStatement.AppendLine("VALUES (" +
                                                       "'" + row["SOURCE_NAME"] + "', " +
                                                       "'" + row["SATELLITE_NAME"] + "', " +
                                                       "'" + row["ATTRIBUTE_NAME_FROM"] + "', " +
                                                       "'" + row["ATTRIBUTE_NAME_TO"] + "', " +
                                                       "'" + row["MULTI_ACTIVE_KEY_INDICATOR"] + "'," +
                                                       "'Automatic mapping'" +
                                                       ")");

                            var command = new SqlCommand(insertStatement.ToString(), connection);

                            try
                            {
                                connection.Open();
                                command.ExecuteNonQuery();
                                automaticMappingCounter++;

                            }
                            catch (Exception ex)
                            {
                                _alert.SetTextLogging("-----> An issue has occurred mapping columns from table " +
                                                      row["SOURCE_NAME"] + " to " + row["SATELLITE_NAME"] +
                                                      ". Please check the Error Log for more details.\r\n");
                                errorCounter++;
                                errorLog.AppendLine(
                                    "\r\nAn issue has occured during preparation of the Source to Satellite attribute mapping: \r\n\r\n" +
                                    ex);
                                errorLog.AppendLine(
                                    "\r\nThe query that caused the issue is: \r\n\r\n" + insertStatement);

                                if (row["ATTRIBUTE_NAME_FROM"].ToString() == "")
                                {
                                    _alert.SetTextLogging("Both attributes are NULL.");
                                }
                            }
                        }
                    }
                }

                worker.ReportProgress(90);
                subProcess.Stop();
                _alert.SetTextLogging("--> Processing " + automaticAttributeMappingsSatellites.Rows.Count +
                                      " automatically added attribute mappings.\r\n");
                _alert.SetTextLogging(
                    "Preparation of the automatically mapped column-to-column metadata completed, and has taken " +
                    subProcess.Elapsed.TotalSeconds + " seconds.\r\n");

                #endregion


                #region Manually mapped degenerate attributes for Links 95%

                //12. Prepare Manual Attribute mapping for Link degenerate fields
                subProcess.Reset();
                subProcess.Start();

                _alert.SetTextLogging("\r\n");
                _alert.SetTextLogging(
                    "Commencing preparing the column-to-column mapping metadata based on the manual mappings for degenerate attributes.\r\n");

                var prepareMappingStatementLink = new StringBuilder();

                prepareMappingStatementLink.AppendLine("SELECT");
                prepareMappingStatementLink.AppendLine("  stg.SOURCE_NAME");
                prepareMappingStatementLink.AppendLine(" ,lnk.LINK_NAME");
                prepareMappingStatementLink.AppendLine(" ,stg_attr.ATTRIBUTE_NAME AS ATTRIBUTE_NAME_FROM");
                prepareMappingStatementLink.AppendLine(" ,target_attr.ATTRIBUTE_NAME AS ATTRIBUTE_NAME_TO");
                prepareMappingStatementLink.AppendLine(" ,'Manual mapping' as MAPPING_TYPE");
                prepareMappingStatementLink.AppendLine("FROM dbo.TMP_MD_ATTRIBUTE_MAPPING mapping");
                prepareMappingStatementLink.AppendLine("LEFT OUTER JOIN dbo.MD_LINK lnk on lnk.[SCHEMA_NAME]+'.'+lnk.LINK_NAME=mapping.TARGET_TABLE");
                prepareMappingStatementLink.AppendLine("LEFT OUTER JOIN dbo.MD_ATTRIBUTE target_attr on mapping.TARGET_COLUMN = target_attr.ATTRIBUTE_NAME");
                prepareMappingStatementLink.AppendLine("LEFT OUTER JOIN dbo.MD_SOURCE stg on stg.[SCHEMA_NAME]+'.'+stg.SOURCE_NAME = mapping.SOURCE_TABLE");
                prepareMappingStatementLink.AppendLine("LEFT OUTER JOIN dbo.MD_ATTRIBUTE stg_attr on mapping.SOURCE_COLUMN = stg_attr.ATTRIBUTE_NAME");
                prepareMappingStatementLink.AppendLine("LEFT OUTER JOIN dbo.TMP_MD_TABLE_MAPPING table_mapping");
                prepareMappingStatementLink.AppendLine("  ON mapping.TARGET_TABLE = table_mapping.TARGET_TABLE");
                prepareMappingStatementLink.AppendLine(" AND mapping.SOURCE_TABLE = table_mapping.SOURCE_TABLE");
                prepareMappingStatementLink.AppendLine("WHERE mapping.TARGET_TABLE_TYPE = ('" +
                                                       MetadataHandling.TableTypes.NaturalBusinessRelationship +
                                                       "')");
                prepareMappingStatementLink.AppendLine("      AND table_mapping.[ENABLED_INDICATOR] = 'True'");

                var degenerateMappings = Utility.GetDataTable(ref connOmd, prepareMappingStatementLink.ToString());

                if (degenerateMappings.Rows.Count == 0)
                {
                    _alert.SetTextLogging("--> No manually mapped degenerate columns were detected.\r\n");
                }

                worker.ReportProgress(95);
                subProcess.Stop();
                _alert.SetTextLogging("--> Processing " + degenerateMappings.Rows.Count +
                                      " manual degenerate attribute mappings.\r\n");
                _alert.SetTextLogging("Preparation of the degenerate column metadata completed, and has taken " +
                                      subProcess.Elapsed.TotalSeconds + " seconds.\r\n");

                #endregion


                #region Automatically mapped degenerate attributes for Links 95%

                //13. Prepare the automatic degenerate attribute mapping
                subProcess.Reset();
                subProcess.Start();

                _alert.SetTextLogging("\r\n");


                int automaticDegenerateMappingCounter = 0;
                var prepareDegenerateMappingStatement = new StringBuilder();

                prepareDegenerateMappingStatement.AppendLine("WITH ALL_DATABASE_COLUMNS AS");
                prepareDegenerateMappingStatement.AppendLine("(");

                prepareDegenerateMappingStatement.AppendLine("SELECT");
                prepareDegenerateMappingStatement.AppendLine("  [DATABASE_NAME]");
                prepareDegenerateMappingStatement.AppendLine(" ,[SCHEMA_NAME]");
                prepareDegenerateMappingStatement.AppendLine(" ,[TABLE_NAME]");
                prepareDegenerateMappingStatement.AppendLine(" ,[COLUMN_NAME]");
                prepareDegenerateMappingStatement.AppendLine(" ,[DATA_TYPE]");
                prepareDegenerateMappingStatement.AppendLine(" ,[CHARACTER_MAXIMUM_LENGTH]");
                prepareDegenerateMappingStatement.AppendLine(" ,[NUMERIC_PRECISION]");
                prepareDegenerateMappingStatement.AppendLine(" ,[ORDINAL_POSITION]");
                prepareDegenerateMappingStatement.AppendLine(" ,[PRIMARY_KEY_INDICATOR]");
                prepareDegenerateMappingStatement.AppendLine("FROM [MD_PHYSICAL_MODEL]");

                prepareDegenerateMappingStatement.AppendLine("),");
                prepareDegenerateMappingStatement.AppendLine("XREF AS");
                prepareDegenerateMappingStatement.AppendLine("(");
                prepareDegenerateMappingStatement.AppendLine("  SELECT");
                prepareDegenerateMappingStatement.AppendLine("    src.[SCHEMA_NAME] AS SOURCE_SCHEMA_NAME,");
                prepareDegenerateMappingStatement.AppendLine("    xref.SOURCE_NAME AS SOURCE_NAME,");
                prepareDegenerateMappingStatement.AppendLine("    lnk.[SCHEMA_NAME] AS TARGET_SCHEMA_NAME,");
                prepareDegenerateMappingStatement.AppendLine("    xref.LINK_NAME AS TARGET_NAME");
                prepareDegenerateMappingStatement.AppendLine("  FROM MD_SOURCE_LINK_XREF xref");
                prepareDegenerateMappingStatement.AppendLine("LEFT OUTER JOIN dbo.MD_SOURCE src ON xref.SOURCE_NAME = src.SOURCE_NAME");
                prepareDegenerateMappingStatement.AppendLine("LEFT OUTER JOIN dbo.MD_LINK lnk ON xref.LINK_NAME = lnk.LINK_NAME");
                prepareDegenerateMappingStatement.AppendLine(") ");
                prepareDegenerateMappingStatement.AppendLine("SELECT");
                prepareDegenerateMappingStatement.AppendLine("  XREF.SOURCE_NAME, ");
                prepareDegenerateMappingStatement.AppendLine("  XREF.TARGET_NAME AS LINK_NAME,");
                prepareDegenerateMappingStatement.AppendLine("  ADC_SOURCE.COLUMN_NAME AS ATTRIBUTE_NAME_FROM,");
                prepareDegenerateMappingStatement.AppendLine("  ADC_TARGET.COLUMN_NAME AS ATTRIBUTE_NAME_TO,");
                prepareDegenerateMappingStatement.AppendLine("  'N' AS MULTI_ACTIVE_INDICATOR,");
                prepareDegenerateMappingStatement.AppendLine("  'Automatic mapping' as MAPPING_TYPE");
                prepareDegenerateMappingStatement.AppendLine("FROM XREF");
                prepareDegenerateMappingStatement.AppendLine("JOIN ALL_DATABASE_COLUMNS ADC_SOURCE ON XREF.SOURCE_SCHEMA_NAME = ADC_SOURCE.[SCHEMA_NAME] AND XREF.SOURCE_NAME = ADC_SOURCE.TABLE_NAME");
                prepareDegenerateMappingStatement.AppendLine("JOIN ALL_DATABASE_COLUMNS ADC_TARGET ON XREF.TARGET_SCHEMA_NAME = ADC_TARGET.[SCHEMA_NAME] AND XREF.TARGET_NAME = ADC_TARGET.TABLE_NAME");
                prepareDegenerateMappingStatement.AppendLine("LEFT OUTER JOIN dbo.MD_ATTRIBUTE stg_attr ON UPPER(ADC_SOURCE.COLUMN_NAME) = UPPER(stg_attr.ATTRIBUTE_NAME) COLLATE DATABASE_DEFAULT");
                prepareDegenerateMappingStatement.AppendLine("LEFT OUTER JOIN dbo.MD_ATTRIBUTE tgt_attr ON UPPER(ADC_TARGET.COLUMN_NAME) = UPPER(tgt_attr.ATTRIBUTE_NAME) COLLATE DATABASE_DEFAULT");
                prepareDegenerateMappingStatement.AppendLine("WHERE stg_attr.ATTRIBUTE_NAME = tgt_attr.ATTRIBUTE_NAME");


                if (radioButtonPhysicalMode.Checked)
                {
                    _alert.SetTextLogging(
                        "Commencing preparing the (automatic) column-to-column mapping metadata for degenerate attributes, based on what's available in the database.\r\n");
                }
                else
                {
                    _alert.SetTextLogging(
                        "Commencing preparing the degenerate column metadata using the physical model metadata.\r\n");
                }

                var automaticDegenerateMappings =
                    Utility.GetDataTable(ref connOmd, prepareDegenerateMappingStatement.ToString());

                if (automaticDegenerateMappings.Rows.Count == 0)
                {
                    _alert.SetTextLogging("--> No automatic degenerate columns were detected.\r\n");
                }
                else
                {
                    // Prevent duplicates to be inserted into the datatable, by only inserting new ones
                    // Entries found in the automatic check which are not already in the manual datatable will be added
                    foreach (DataRow automaticMapping in automaticDegenerateMappings.Rows)
                    {
                        DataRow[] foundRow = degenerateMappings.Select(
                            "SOURCE_NAME = '" + automaticMapping["SOURCE_NAME"] + "' AND LINK_NAME = '" +
                            automaticMapping["LINK_NAME"] + "' AND ATTRIBUTE_NAME_FROM = '" +
                            automaticMapping["ATTRIBUTE_NAME_FROM"] + "'AND ATTRIBUTE_NAME_TO = '" +
                            automaticMapping["ATTRIBUTE_NAME_TO"] + "'");
                        if (foundRow.Length == 0)
                        {
                            // If nothing is found, add to the overall data table that is inserted into SOURCE_SATELLITE_ATTRIBUTE_XREF
                            degenerateMappings.Rows.Add(
                                automaticMapping["SOURCE_NAME"],
                                automaticMapping["LINK_NAME"],
                                automaticMapping["ATTRIBUTE_NAME_FROM"],
                                automaticMapping["ATTRIBUTE_NAME_TO"],
                                automaticMapping["MAPPING_TYPE"]);

                            automaticDegenerateMappingCounter++;
                        }
                    }
                }

                // Now the full data table can be processed
                if (degenerateMappings.Rows.Count > 0)
                {
                    foreach (DataRow tableName in degenerateMappings.Rows)
                    {
                        using (var connection = new SqlConnection(metaDataConnection))
                        {

                            var insertStatement = new StringBuilder();

                            insertStatement.AppendLine("INSERT INTO [MD_SOURCE_LINK_ATTRIBUTE_XREF]");
                            insertStatement.AppendLine("( [SOURCE_NAME],[LINK_NAME],[ATTRIBUTE_NAME_FROM],[ATTRIBUTE_NAME_TO], [MAPPING_TYPE])");
                            insertStatement.AppendLine("VALUES (" +
                                                       "'" + tableName["SOURCE_NAME"] + "', " +
                                                       "'" + tableName["LINK_NAME"] + "', " +
                                                       "'" + tableName["ATTRIBUTE_NAME_FROM"] + "', " +
                                                       "'" + tableName["ATTRIBUTE_NAME_TO"] + "', " +
                                                       "'" + tableName["MAPPING_TYPE"] + "'" +
                                                       ")");

                            var command = new SqlCommand(insertStatement.ToString(), connection);

                            try
                            {
                                connection.Open();
                                command.ExecuteNonQuery();

                            }
                            catch (Exception)
                            {
                                _alert.SetTextLogging(
                                    "-----> An issue has occurred mapping degenerate columns from table " +
                                    tableName["SOURCE_NAME"] + " to " + tableName["LINK_NAME"] +
                                    ". Please check the Error Log for more details.\r\n");
                                errorCounter++;
                                errorLog.AppendLine(
                                    "\r\nThe query that caused the issue is: \r\n\r\n" + insertStatement);
                                if (tableName["ATTRIBUTE_NAME_FROM"].ToString() == "")
                                {
                                    _alert.SetTextLogging("Both attributes are NULL.");
                                }
                            }
                        }
                    }
                }

                worker.ReportProgress(95);
                subProcess.Stop();
                _alert.SetTextLogging("--> Processing " + automaticDegenerateMappingCounter +
                                      " automatically added degenerate attribute mappings.\r\n");
                _alert.SetTextLogging("Preparation of the degenerate column metadata completed, and has taken " +
                                      subProcess.Elapsed.TotalSeconds.ToString() + " seconds.\r\n");

                #endregion


                #region Multi-Active Key - 97%

                //Handle the Multi-Active Key
                subProcess.Reset();
                subProcess.Start();

                _alert.SetTextLogging("\r\n");


                var prepareMultiKeyStatement = new StringBuilder();

                if (radioButtonPhysicalMode.Checked)
                {
                    _alert.SetTextLogging("Commencing Multi-Active Key handling using database.\r\n");

                    prepareMultiKeyStatement.AppendLine("SELECT");
                    prepareMultiKeyStatement.AppendLine("   xref.SOURCE_NAME");
                    prepareMultiKeyStatement.AppendLine("  ,xref.SATELLITE_NAME");
                    prepareMultiKeyStatement.AppendLine("  ,xref.ATTRIBUTE_NAME_FROM");
                    prepareMultiKeyStatement.AppendLine("  ,xref.ATTRIBUTE_NAME_TO");
                    prepareMultiKeyStatement.AppendLine("FROM MD_SOURCE_SATELLITE_ATTRIBUTE_XREF xref");
                    prepareMultiKeyStatement.AppendLine("INNER JOIN ");
                    prepareMultiKeyStatement.AppendLine("(");

                    //prepareMultiKeyStatement.AppendLine("  SELECT ");
                    //prepareMultiKeyStatement.AppendLine("  	sc.name AS SATELLITE_NAME,");
                    //prepareMultiKeyStatement.AppendLine("  	C.name AS ATTRIBUTE_NAME");
                    //prepareMultiKeyStatement.AppendLine("  FROM " + linkedServer + integrationDatabase + ".sys.index_columns A");
                    //prepareMultiKeyStatement.AppendLine("  JOIN " + linkedServer + integrationDatabase + ".sys.indexes B");
                    //prepareMultiKeyStatement.AppendLine("    ON A.OBJECT_ID=B.OBJECT_ID AND A.index_id=B.index_id");
                    //prepareMultiKeyStatement.AppendLine("  JOIN " + linkedServer + integrationDatabase + ".sys.columns C");
                    //prepareMultiKeyStatement.AppendLine("    ON A.column_id=C.column_id AND A.OBJECT_ID=C.OBJECT_ID");
                    //prepareMultiKeyStatement.AppendLine("  JOIN " + linkedServer + integrationDatabase + ".sys.tables sc on sc.OBJECT_ID = A.OBJECT_ID");
                    //prepareMultiKeyStatement.AppendLine("    WHERE is_primary_key=1");
                    //prepareMultiKeyStatement.AppendLine("  AND C.name!='" + effectiveDateTimeAttribute + "' AND C.name!='" + currentRecordAttribute + "' AND C.name!='" + eventDateTimeAtttribute + "'");
                    //prepareMultiKeyStatement.AppendLine("  AND C.name NOT LIKE '" + dwhKeyIdentifier + "'");

                    prepareMultiKeyStatement.AppendLine("  SELECT");
                    prepareMultiKeyStatement.AppendLine("    [SCHEMA_NAME] AS LINK_SCHEMA,");
                    prepareMultiKeyStatement.AppendLine("    [TABLE_NAME]  AS SATELLITE_NAME,");
                    prepareMultiKeyStatement.AppendLine("    [COLUMN_NAME] AS ATTRIBUTE_NAME");
                    prepareMultiKeyStatement.AppendLine("  FROM MD_PHYSICAL_MODEL");
                    prepareMultiKeyStatement.AppendLine("  WHERE ");
                    prepareMultiKeyStatement.AppendLine("        COLUMN_NAME != '" + effectiveDateTimeAttribute + "' AND COLUMN_NAME != '" + currentRecordAttribute + "' AND COLUMN_NAME != '" + eventDateTimeAtttribute + "'");
                    prepareMultiKeyStatement.AppendLine("    AND COLUMN_NAME NOT LIKE '" + dwhKeyIdentifier + "'");
                    prepareMultiKeyStatement.AppendLine("    AND (TABLE_NAME LIKE '" + satTablePrefix + "' OR TABLE_NAME LIKE '" + lsatTablePrefix + "')");
                    prepareMultiKeyStatement.AppendLine("    AND PRIMARY_KEY_INDICATOR='Y'");

                    prepareMultiKeyStatement.AppendLine(") ddsub");
                    prepareMultiKeyStatement.AppendLine("ON xref.SATELLITE_NAME = ddsub.SATELLITE_NAME");
                    prepareMultiKeyStatement.AppendLine("AND xref.ATTRIBUTE_NAME_TO = ddsub.ATTRIBUTE_NAME");
                    prepareMultiKeyStatement.AppendLine("  WHERE ddsub.SATELLITE_NAME LIKE '" + satTablePrefix + "' OR ddsub.SATELLITE_NAME LIKE '" + lsatTablePrefix + "'");

                }
                else
                {
                    _alert.SetTextLogging("Commencing Multi-Active Key handling using model metadata.\r\n");

                    prepareMultiKeyStatement.AppendLine("SELECT ");
                    prepareMultiKeyStatement.AppendLine("   xref.SOURCE_NAME");
                    prepareMultiKeyStatement.AppendLine("  ,xref.SATELLITE_NAME");
                    prepareMultiKeyStatement.AppendLine("  ,xref.ATTRIBUTE_NAME_FROM");
                    prepareMultiKeyStatement.AppendLine("  ,xref.ATTRIBUTE_NAME_TO");
                    prepareMultiKeyStatement.AppendLine("FROM MD_SOURCE_SATELLITE_ATTRIBUTE_XREF xref");
                    prepareMultiKeyStatement.AppendLine("INNER JOIN ");
                    prepareMultiKeyStatement.AppendLine("(");
                    prepareMultiKeyStatement.AppendLine("	SELECT");
                    prepareMultiKeyStatement.AppendLine("		TABLE_NAME AS SATELLITE_NAME,");
                    prepareMultiKeyStatement.AppendLine("		COLUMN_NAME AS ATTRIBUTE_NAME");
                    prepareMultiKeyStatement.AppendLine("	FROM TMP_MD_VERSION_ATTRIBUTE");
                    prepareMultiKeyStatement.AppendLine("	WHERE MULTI_ACTIVE_INDICATOR='Y'");
                    prepareMultiKeyStatement.AppendLine(") sub");
                    prepareMultiKeyStatement.AppendLine("ON xref.SATELLITE_NAME = sub.SATELLITE_NAME");
                    prepareMultiKeyStatement.AppendLine("AND xref.ATTRIBUTE_NAME_TO = sub.ATTRIBUTE_NAME");
                }

                var listMultiKeys = Utility.GetDataTable(ref connOmd, prepareMultiKeyStatement.ToString());

                if (listMultiKeys == null || listMultiKeys.Rows.Count == 0)
                {
                    _alert.SetTextLogging("--> No Multi-Active Keys were detected.\r\n");
                }
                else
                {
                    foreach (DataRow tableName in listMultiKeys.Rows)
                    {
                        using (var connection = new SqlConnection(metaDataConnection))
                        {
                            _alert.SetTextLogging("--> Processing the Multi-Active Key attribute " +
                                                  tableName["ATTRIBUTE_NAME_TO"] + " for " +
                                                  tableName["SATELLITE_NAME"] + ".\r\n");

                            var insertStatement = new StringBuilder();
                            insertStatement.AppendLine("UPDATE [MD_SOURCE_SATELLITE_ATTRIBUTE_XREF]");
                            insertStatement.AppendLine("SET MULTI_ACTIVE_KEY_INDICATOR='Y'");
                            insertStatement.AppendLine("WHERE SOURCE_NAME = '" + tableName["SOURCE_NAME"] + "'");
                            insertStatement.AppendLine("AND SATELLITE_NAME = '" + tableName["SATELLITE_NAME"] + "'");
                            insertStatement.AppendLine("AND ATTRIBUTE_NAME_FROM = '" +
                                                       tableName["ATTRIBUTE_NAME_FROM"] + "'");
                            insertStatement.AppendLine("AND ATTRIBUTE_NAME_TO = '" + tableName["ATTRIBUTE_NAME_TO"] +
                                                       "'");


                            var command = new SqlCommand(insertStatement.ToString(), connection);

                            try
                            {
                                connection.Open();
                                command.ExecuteNonQuery();
                            }
                            catch (Exception ex)
                            {
                                errorCounter++;
                                _alert.SetTextLogging(
                                    "An issue has occured during preparation of the Multi-Active key metadata. Please check the Error Log for more details.\r\n");

                                errorLog.AppendLine(
                                    "\r\nAn issue has occured during preparation of the Multi-Active key metadata: \r\n\r\n" +
                                    ex);
                                errorLog.AppendLine(
                                    "\r\nThe query that caused the issue is: \r\n\r\n" + insertStatement);
                            }
                        }
                    }
                }

                worker.ReportProgress(97);
                subProcess.Stop();
                _alert.SetTextLogging($"Preparation of the Multi-Active Keys completed, and has taken {subProcess.Elapsed.TotalSeconds} seconds.\r\n");

                #endregion


                #region Driving Key preparation

                //Prepare driving keys
                subProcess.Reset();
                subProcess.Start();

                _alert.SetTextLogging("\r\n");
                _alert.SetTextLogging("Commencing preparing the Driving Key metadata.\r\n");


                var prepareDrivingKeyStatement = new StringBuilder();
                prepareDrivingKeyStatement.AppendLine("SELECT DISTINCT");
                prepareDrivingKeyStatement.AppendLine("         sat.SATELLITE_NAME");
                prepareDrivingKeyStatement.AppendLine(
                    "         ,COALESCE(hubkey.HUB_NAME, (SELECT HUB_NAME FROM MD_HUB WHERE HUB_NAME = 'Not applicable')) AS HUB_NAME");
                prepareDrivingKeyStatement.AppendLine(" FROM");
                prepareDrivingKeyStatement.AppendLine(" (");
                prepareDrivingKeyStatement.AppendLine("         SELECT");
                prepareDrivingKeyStatement.AppendLine("                 SOURCE_TABLE,");
                prepareDrivingKeyStatement.AppendLine("                 TARGET_TABLE,");
                prepareDrivingKeyStatement.AppendLine("                 VERSION_ID,");
                prepareDrivingKeyStatement.AppendLine("                 CASE");
                prepareDrivingKeyStatement.AppendLine(
                    "                         WHEN CHARINDEX('(', RTRIM(LTRIM(Split.a.value('.', 'VARCHAR(MAX)')))) > 0");
                prepareDrivingKeyStatement.AppendLine(
                    "                         THEN RTRIM(LTRIM(Split.a.value('.', 'VARCHAR(MAX)')))");
                prepareDrivingKeyStatement.AppendLine(
                    "                         ELSE REPLACE(RTRIM(LTRIM(Split.a.value('.', 'VARCHAR(MAX)'))), ')', '')");
                prepareDrivingKeyStatement.AppendLine(
                    "                 END AS BUSINESS_KEY_ATTRIBUTE--For Driving Key");
                prepareDrivingKeyStatement.AppendLine("         FROM");
                prepareDrivingKeyStatement.AppendLine("         (");
                prepareDrivingKeyStatement.AppendLine(
                    "                 SELECT SOURCE_TABLE, TARGET_TABLE, DRIVING_KEY_ATTRIBUTE, VERSION_ID, CONVERT(XML, '<M>' + REPLACE(DRIVING_KEY_ATTRIBUTE, ',', '</M><M>') + '</M>') AS DRIVING_KEY_ATTRIBUTE_XML");
                prepareDrivingKeyStatement.AppendLine("                 FROM");
                prepareDrivingKeyStatement.AppendLine("                 (");
                prepareDrivingKeyStatement.AppendLine(
                    "                         SELECT DISTINCT SOURCE_TABLE, TARGET_TABLE, VERSION_ID, LTRIM(RTRIM(DRIVING_KEY_ATTRIBUTE)) AS DRIVING_KEY_ATTRIBUTE");
                prepareDrivingKeyStatement.AppendLine("                         FROM TMP_MD_TABLE_MAPPING");
                prepareDrivingKeyStatement.AppendLine("                         WHERE TARGET_TABLE_TYPE IN ('" +
                                                      MetadataHandling.TableTypes
                                                          .NaturalBusinessRelationshipContext +
                                                      "') AND DRIVING_KEY_ATTRIBUTE IS NOT NULL AND DRIVING_KEY_ATTRIBUTE != ''");
                prepareDrivingKeyStatement.AppendLine("                         AND [ENABLED_INDICATOR] = 'True'");
                prepareDrivingKeyStatement.AppendLine("                 ) TableName");
                prepareDrivingKeyStatement.AppendLine(
                    "         ) AS A CROSS APPLY DRIVING_KEY_ATTRIBUTE_XML.nodes('/M') AS Split(a)");
                prepareDrivingKeyStatement.AppendLine(" )  base");
                prepareDrivingKeyStatement.AppendLine(" LEFT JOIN [dbo].[TMP_MD_TABLE_MAPPING]");
                prepareDrivingKeyStatement.AppendLine("         hub");
                prepareDrivingKeyStatement.AppendLine("     ON  base.SOURCE_TABLE=hub.SOURCE_TABLE");
                prepareDrivingKeyStatement.AppendLine("     AND hub.TARGET_TABLE_TYPE IN ('" +
                                                      MetadataHandling.TableTypes.CoreBusinessConcept + "')");
                prepareDrivingKeyStatement.AppendLine(
                    "     AND base.BUSINESS_KEY_ATTRIBUTE=hub.BUSINESS_KEY_ATTRIBUTE");
                prepareDrivingKeyStatement.AppendLine(" LEFT JOIN MD_SATELLITE sat");
                prepareDrivingKeyStatement.AppendLine(
                    "     ON base.TARGET_TABLE = sat.[SCHEMA_NAME]+'.'+sat.SATELLITE_NAME");
                prepareDrivingKeyStatement.AppendLine(" LEFT JOIN MD_HUB hubkey");
                prepareDrivingKeyStatement.AppendLine(
                    "     ON hub.TARGET_TABLE = hubkey.[SCHEMA_NAME]+'.'+hubkey.HUB_NAME");
                prepareDrivingKeyStatement.AppendLine(" WHERE 1=1");
                prepareDrivingKeyStatement.AppendLine(" AND base.BUSINESS_KEY_ATTRIBUTE IS NOT NULL");
                prepareDrivingKeyStatement.AppendLine(" AND base.BUSINESS_KEY_ATTRIBUTE!=''");
                prepareDrivingKeyStatement.AppendLine(" AND [ENABLED_INDICATOR] = 'True'");


                var listDrivingKeys = Utility.GetDataTable(ref connOmd, prepareDrivingKeyStatement.ToString());

                if (listDrivingKeys.Rows.Count == 0)
                {
                    _alert.SetTextLogging("--> No Driving Key based Link-Satellites were detected.\r\n");
                }
                else
                {
                    foreach (DataRow tableName in listDrivingKeys.Rows)
                    {
                        using (var connection = new SqlConnection(metaDataConnection))
                        {
                            var insertStatement = new StringBuilder();

                            insertStatement.AppendLine("INSERT INTO [MD_DRIVING_KEY_XREF]");
                            insertStatement.AppendLine("( [SATELLITE_NAME] ,[HUB_NAME] )");
                            insertStatement.AppendLine("VALUES ");
                            insertStatement.AppendLine("(");
                            insertStatement.AppendLine("  '" + tableName["SATELLITE_NAME"] + "',");
                            insertStatement.AppendLine("  '" + tableName["HUB_NAME"] + "'");
                            insertStatement.AppendLine(")");

                            var command = new SqlCommand(insertStatement.ToString(), connection);

                            try
                            {
                                connection.Open();
                                command.ExecuteNonQuery();
                            }
                            catch (Exception ex)
                            {
                                errorCounter++;
                                _alert.SetTextLogging(
                                    "An issue has occured during preparation of the Driving Key metadata. Please check the Error Log for more details.\r\n");

                                errorLog.AppendLine(
                                    "\r\nAn issue has occured during preparation of the Driving Key metadata: \r\n\r\n" +
                                    ex);
                                errorLog.AppendLine(
                                    "\r\nThe query that caused the issue is: \r\n\r\n" + insertStatement);
                            }
                        }
                    }
                }

                worker.ReportProgress(98);
                subProcess.Stop();
                _alert.SetTextLogging("Preparation of the Driving Key column metadata completed, and has taken " +
                                      subProcess.Elapsed.TotalSeconds.ToString() + " seconds.\r\n");

                #endregion

                //
                // Activation completed!
                //

                // Report the events (including errors) back to the user
                // Clear out the existing error log, or create an empty new file
                //using (var outfile = new StreamWriter(GlobalParameters.ConfigurationPath + @"\Event_Log.txt"))
                //{
                //    outfile.Write(String.Empty);
                //    outfile.Close();
                //}

                //int eventErrorCounter = 0;
                //StringBuilder logOutput = new StringBuilder();
                //foreach (Event individualEvent in eventLog)
                //{
                //    if (individualEvent.eventCode == (int)EventTypes.Error)
                //    {
                //        eventErrorCounter++;
                //    }

                //    logOutput.AppendLine((EventTypes)individualEvent.eventCode + ": " + individualEvent.eventDescription);
                //}

                //using (var outfile = new StreamWriter(GlobalParameters.ConfigurationPath + @"\Event_Log.txt"))
                //{
                //    outfile.Write(logOutput.ToString());
                //    outfile.Close();
                //}

                // Error handling
                // Clear out the existing error log, or create an empty new file
                using (var outfile = new StreamWriter(GlobalParameters.ConfigurationPath + @"\Error_Log.txt"))
                {
                    outfile.Write(String.Empty);
                    outfile.Close();
                }

                // Write any errors
                if (errorCounter > 0)
                {
                    _alert.SetTextLogging("\r\nWarning! There were " + errorCounter +
                                          " error(s) found while processing the metadata.\r\n");
                    _alert.SetTextLogging("Please check the Error Log for details \r\n");
                    _alert.SetTextLogging("\r\n");

                    using (var outfile = new StreamWriter(GlobalParameters.ConfigurationPath + @"\Error_Log.txt"))
                    {
                        outfile.Write(errorLog.ToString());
                        outfile.Close();
                    }
                }
                else
                {
                    _alert.SetTextLogging("\r\nNo errors were detected.\r\n");
                }

                // Remove the temporary tables that have been used
                droptemporaryWorkerTable(TeamConfigurationSettings.MetadataConnection.CreateSqlServerConnectionString(false));

                // Report completion
                totalProcess.Stop();
                _alert.SetTextLogging("\r\n\r\nThe full activation process has taken "+totalProcess.Elapsed.TotalSeconds+" seconds.");
                worker.ReportProgress(100);
            }
        }
        
        private void DataGridViewTableMetadataKeyDown(object sender, KeyEventArgs e)
        {
            // Only works when not in edit mode.
            try
            {
                if (e.Modifiers == Keys.Control)
                {
                    switch (e.KeyCode)
                    {
                        case Keys.V:
                            PasteClipboardTableMetadata();
                            break;
                        case Keys.C:
                            if (sender.GetType() == typeof(DataGridViewComboBoxEditingControl))
                            {
                                var temp = (DataGridViewComboBoxEditingControl) sender;
                                Clipboard.SetText(temp.SelectedValue.ToString());
                            }

                            break;
                    }
                }
            }
            catch (Exception)
            {
                MessageBox.Show("Pasting into the data grid has failed", "Copy/Paste", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        /// <summary>
        /// DataGridView OnKeyDown event for DataGridViewAttributeMetadata
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DataGridViewAttributeMetadataKeyDown(object sender, KeyEventArgs e)
        {
            try
            {
                if (e.Modifiers == Keys.Control)
                {
                    switch (e.KeyCode)
                    {
                        case Keys.V:
                            PasteClipboardAttributeMetadata();
                            break;
                    }
                }
            }
            catch (Exception)
            {
                MessageBox.Show("Pasting into the data grid has failed", "Copy/Paste", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void PasteClipboardTableMetadata()
        {
            try
            {
                string s = Clipboard.GetText();
                string[] lines = s.Split('\n');

                int iRow = dataGridViewTableMetadata.CurrentCell.RowIndex;
                int iCol = dataGridViewTableMetadata.CurrentCell.ColumnIndex;
                DataGridViewCell oCell;
                if (iRow + lines.Length > dataGridViewTableMetadata.Rows.Count - 1)
                {
                    bool bFlag = false;
                    foreach (string sEmpty in lines)
                    {
                        if (sEmpty == "")
                        {
                            bFlag = true;
                        }
                    }

                    int iNewRows = iRow + lines.Length - dataGridViewTableMetadata.Rows.Count;
                    if (iNewRows > 0)
                    {
                        if (bFlag)
                            dataGridViewTableMetadata.Rows.Add(iNewRows);
                        else
                            dataGridViewTableMetadata.Rows.Add(iNewRows + 1);
                    }
                    else
                        dataGridViewTableMetadata.Rows.Add(iNewRows + 1);
                }
                foreach (string line in lines)
                {
                    if (iRow < dataGridViewTableMetadata.RowCount && line.Length > 0)
                    {
                        string[] sCells = line.Split('\t');
                        for (int i = 0; i < sCells.GetLength(0); ++i)
                        {
                            if (iCol + i < dataGridViewTableMetadata.ColumnCount)
                            {
                                oCell = dataGridViewTableMetadata[iCol + i, iRow];
                                oCell.Value = Convert.ChangeType(sCells[i].Replace("\r", ""), oCell.ValueType);
                            }
                            else
                            {
                                break;
                            }
                        }
                        iRow++;
                    }
                    else
                    {
                        break;
                    }
                }
                //Clipboard.Clear();
            }
            catch (FormatException)
            {
                MessageBox.Show("There is an issue with the data format for this cell!");
            }
        }

        private void PasteClipboardAttributeMetadata()
        {
            try
            {
                string s = Clipboard.GetText();
                string[] lines = s.Split('\n');

                int iRow = dataGridViewAttributeMetadata.CurrentCell.RowIndex;
                int iCol = dataGridViewAttributeMetadata.CurrentCell.ColumnIndex;
                DataGridViewCell oCell;
                if (iRow + lines.Length > dataGridViewAttributeMetadata.Rows.Count - 1)
                {
                    bool bFlag = false;
                    foreach (string sEmpty in lines)
                    {
                        if (sEmpty == "")
                        {
                            bFlag = true;
                        }
                    }

                    int iNewRows = iRow + lines.Length - dataGridViewAttributeMetadata.Rows.Count;
                    if (iNewRows > 0)
                    {
                        if (bFlag)
                            dataGridViewAttributeMetadata.Rows.Add(iNewRows);
                        else
                            dataGridViewAttributeMetadata.Rows.Add(iNewRows + 1);
                    }
                    else
                        dataGridViewAttributeMetadata.Rows.Add(iNewRows + 1);
                }
                foreach (string line in lines)
                {
                    if (iRow < dataGridViewAttributeMetadata.RowCount && line.Length > 0)
                    {
                        string[] sCells = line.Split('\t');
                        for (int i = 0; i < sCells.GetLength(0); ++i)
                        {
                            if (iCol + i < dataGridViewAttributeMetadata.ColumnCount)
                            {
                                oCell = dataGridViewAttributeMetadata[iCol + i, iRow];
                                oCell.Value = Convert.ChangeType(sCells[i].Replace("\r", ""), oCell.ValueType);
                            }
                            else
                            {
                                break;
                            }
                        }
                        iRow++;
                    }
                    else
                    {
                        break;
                    }
                }
                //Clipboard.Clear();
            }
            catch (FormatException)
            {
                MessageBox.Show("There is an issue with the data format for this cell!");
            }
        }

        private void FormManageMetadata_SizeChanged(object sender, EventArgs e)
        {
            GridAutoLayout();
        }

        /// <summary>
        /// Validation event on Table Metadata datagridview.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void dataGridViewTableMetadata_CellValidating(object sender, DataGridViewCellValidatingEventArgs e)
        {
            // Validate the data entry on the Table Mapping datagridview
            var valueLength = e.FormattedValue.ToString().Length;
            
            // Source Table (Source)
            if (e.ColumnIndex == (int)TableMappingMetadataColumns.SourceTable)
            {
                dataGridViewTableMetadata.Rows[e.RowIndex].ErrorText = "";

                if (e.FormattedValue == DBNull.Value || valueLength == 0)
                {
                    e.Cancel = true;
                    dataGridViewTableMetadata.Rows[e.RowIndex].ErrorText = "The Source (Source) table cannot be empty!";
                }
            }

            // Target Table
            if (e.ColumnIndex == (int)TableMappingMetadataColumns.TargetTable)
            {
                dataGridViewTableMetadata.Rows[e.RowIndex].ErrorText = "";

                if (e.FormattedValue == DBNull.Value || valueLength == 0)
                {
                    e.Cancel = true;
                    dataGridViewTableMetadata.Rows[e.RowIndex].ErrorText = "The Target (Integration Layer) table cannot be empty!";
                }
            }

            // Business Key
            if (e.ColumnIndex == (int)TableMappingMetadataColumns.BusinessKeyDefinition)
            {
                dataGridViewTableMetadata.Rows[e.RowIndex].ErrorText = "";

                if (e.FormattedValue == DBNull.Value || valueLength == 0)
                {
                    e.Cancel = true;
                    dataGridViewTableMetadata.Rows[e.RowIndex].ErrorText = "The Business Key cannot be empty!";
                }
            }

            // Filter criteria
            if (e.ColumnIndex == (int)TableMappingMetadataColumns.FilterCriterion)
            {
                dataGridViewTableMetadata.Rows[e.RowIndex].ErrorText = "";
                //int newInteger;
                var equalSignIndex = e.FormattedValue.ToString().IndexOf('=') + 1;

                if (valueLength > 0 && valueLength < 3)
                {
                    e.Cancel = true;
                    dataGridViewTableMetadata.Rows[e.RowIndex].ErrorText = "The filter criterion cannot only be just one or two characters as it translates into a WHERE clause.";
                }

                if (valueLength > 0)
                {
                    //Check if an '=' is there
                    if (e.FormattedValue.ToString() == "=")
                    {
                        e.Cancel = true;
                        dataGridViewTableMetadata.Rows[e.RowIndex].ErrorText = "The filter criterion cannot only be '=' as it translates into a WHERE clause.";
                    }

                    // If there are value in the filter, and the filter contains an equal sign but it's the last then cancel
                    if (valueLength > 2 && (e.FormattedValue.ToString().Contains("=") && !(equalSignIndex < valueLength)))
                    {
                        e.Cancel = true;
                        dataGridViewTableMetadata.Rows[e.RowIndex].ErrorText = "The filter criterion include values either side of the '=' sign as it is expressed as a WHERE clause.";
                    }
                }
            }
        }

        public DateTime ActivationMetadata()
        {
            DateTime mostRecentActivationDateTime = DateTime.MinValue; 

            var connOmd = new SqlConnection { ConnectionString = TeamConfigurationSettings.MetadataConnection.CreateSqlServerConnectionString(false) };

            var sqlStatementForActivationMetadata = new StringBuilder();
            sqlStatementForActivationMetadata.AppendLine("SELECT [VERSION_NAME], MAX([ACTIVATION_DATETIME]) AS [ACTIVATION_DATETIME]");
            sqlStatementForActivationMetadata.AppendLine("FROM [dbo].[MD_MODEL_METADATA]");
            sqlStatementForActivationMetadata.AppendLine("GROUP BY [VERSION_NAME]");

            var activationMetadata = Utility.GetDataTable(ref connOmd, sqlStatementForActivationMetadata.ToString());

            if (activationMetadata != null)
            {
                foreach (DataRow row in activationMetadata.Rows)
                {
                    mostRecentActivationDateTime = (DateTime) row["ACTIVATION_DATETIME"];
                } 
            }

            return mostRecentActivationDateTime;
        }



        private void saveAsDirectionalGraphMarkupLanguageDGMLToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DateTime activationDateTime = ActivationMetadata();

            if (activationDateTime == DateTime.MinValue)
            {
                richTextBoxInformation.Text = "The metadata was not activated, so the graph is constructed only from the raw mappings.";
            }
            else
            {
                richTextBoxInformation.Text = $"DGML will be generated following the most recent activation metadata, as per ({activationDateTime}).";
            }

            var theDialog = new SaveFileDialog
            {
                Title = @"Save Metadata As Directional Graph File",
                Filter = @"DGML files|*.dgml",
                InitialDirectory = Application.StartupPath + @"\Configuration\"
            };

            var ret = STAShowDialog(theDialog);


            if (ret == DialogResult.OK)
            {
                var chosenFile = theDialog.FileName;

                var errorLog = new StringBuilder();
                var errorCounter = 0;

                if (dataGridViewTableMetadata != null) // There needs to be metadata available
                {
                    var connOmd = new SqlConnection {ConnectionString = TeamConfigurationSettings.MetadataConnection.CreateSqlServerConnectionString(false) };

                    //Write the DGML file
                    var dgmlExtract = new StringBuilder();
                    dgmlExtract.AppendLine("<?xml version=\"1.0\" encoding=\"utf - 8\"?>");
                    dgmlExtract.AppendLine("<DirectedGraph ZoomLevel=\" - 1\" xmlns=\"http://schemas.microsoft.com/vs/2009/dgml\">");

                    #region Table nodes
                    //Build up the list of nodes based on the data grid
                    List<string> nodeList = new List<string>();

                    for (int i = 0; i < dataGridViewTableMetadata.Rows.Count - 1; i++)
                    {
                        DataGridViewRow row = dataGridViewTableMetadata.Rows[i];
                        string sourceNode = row.Cells[(int)TableMappingMetadataColumns.SourceTable].Value.ToString();
                        string targetNode = row.Cells[(int)TableMappingMetadataColumns.TargetTable].Value.ToString();

                        // Add source tables to Node List
                        if (!nodeList.Contains(sourceNode))
                        {
                            nodeList.Add(sourceNode);
                        }

                        // Add target tables to Node List
                        if (!nodeList.Contains(targetNode))
                        {
                            nodeList.Add(targetNode);
                        }
                    }

                    dgmlExtract.AppendLine("  <Nodes>");

                    var edgeBuilder = new StringBuilder(); // Also create the links while iterating through the below set
                    
                    foreach (string node in nodeList)
                    {
                        if (node.Contains(TeamConfigurationSettings.StgTablePrefixValue))
                        {
                            dgmlExtract.AppendLine("     <Node Id=\"" + node + "\"  Category=\"Landing Area\" Group=\"Collapsed\" Label=\"" + node + "\" />");
                            edgeBuilder.AppendLine("     <Link Source=\"Staging Layer\" Target=\"" + node + "\" Category=\"Contains\" />");
                        }
                        else if (node.Contains(TeamConfigurationSettings.PsaTablePrefixValue))
                        {
                            dgmlExtract.AppendLine("     <Node Id=\"" + node + "\"  Category=\"Persistent Staging Area\" Group=\"Collapsed\" Label=\"" + node + "\" />");
                            edgeBuilder.AppendLine("     <Link Source=\"Staging Layer\" Target=\"" + node + "\" Category=\"Contains\" />");
                        }
                        else if (node.Contains(TeamConfigurationSettings.HubTablePrefixValue))
                        {
                            dgmlExtract.AppendLine("     <Node Id=\"" + node + "\"  Category=\"Hub\"  Label=\"" + node + "\" />");
                            //edgeBuilder.AppendLine("     <Link Source=\"Data Vault\" Target=\"" + node + "\" Category=\"Contains\" />");
                        }
                        else if (node.Contains(TeamConfigurationSettings.LinkTablePrefixValue))
                        {
                            dgmlExtract.AppendLine("     <Node Id=\"" + node + "\"  Category=\"Link\" Label=\"" +node + "\" />");
                            //edgeBuilder.AppendLine("     <Link Source=\"Data Vault\" Target=\"" + node + "\" Category=\"Contains\" />");
                        }
                        else if (node.Contains(TeamConfigurationSettings.SatTablePrefixValue) || node.Contains(TeamConfigurationSettings.LsatTablePrefixValue))
                        {
                            dgmlExtract.AppendLine("     <Node Id=\"" + node +"\"  Category=\"Satellite\" Group=\"Collapsed\" Label=\"" +node + "\" />");
                            //edgeBuilder.AppendLine("     <Link Source=\"Data Vault\" Target=\"" + node + "\" Category=\"Contains\" />");
                        }
                        else
                        {
                            dgmlExtract.AppendLine("     <Node Id=\"" + node + "\"  Category=\"Sources\" Label=\"" + node + "\" />");
                            edgeBuilder.AppendLine("     <Link Source=\"Sources\" Target=\"" + node + "\" Category=\"Contains\" />");
                        }
                    }
                    #endregion
                    
                    #region Attribute nodes
                    // Separate routine for attribute nodes, with some additional logic to allow for 'duplicate' nodes e.g. source and target attribute names
                    var sqlStatementForSatelliteAttributes = new StringBuilder();
                    sqlStatementForSatelliteAttributes.AppendLine("SELECT *");
                    sqlStatementForSatelliteAttributes.AppendLine("FROM [interface].[INTERFACE_SOURCE_SATELLITE_ATTRIBUTE_XREF]");

                    var satelliteAttributes = Utility.GetDataTable(ref connOmd, sqlStatementForSatelliteAttributes.ToString());
                    foreach (DataRow row in satelliteAttributes.Rows)
                    {
                        var sourceNodeLabel = (string) row["SOURCE_ATTRIBUTE_NAME"];
                        var sourceNode = "staging_" + sourceNodeLabel;
                        var targetNodeLabel = (string) row["TARGET_ATTRIBUTE_NAME"];
                        var targetNode = "dwh_" + targetNodeLabel;

                        // Add source tables to Node List
                        if (!nodeList.Contains(sourceNode))
                        {
                            nodeList.Add(sourceNode);
                        }

                        // Add target tables to Node List
                        if (!nodeList.Contains(targetNode))
                        {
                            nodeList.Add(targetNode);
                        }

                        dgmlExtract.AppendLine("     <Node Id=\"" + sourceNode + "\"  Category=\"Attribute\" Label=\"" + sourceNodeLabel + "\" />");
                        dgmlExtract.AppendLine("     <Node Id=\"" + targetNode + "\"  Category=\"Attribute\" Label=\"" + targetNodeLabel + "\" />");
                    }
                    #endregion

                    #region Category nodes
                    //Adding the category nodes
                    dgmlExtract.AppendLine("     <Node Id=\"Sources\" Group=\"Collapsed\" Label=\"Sources\"/>");
                    dgmlExtract.AppendLine("     <Node Id=\"Staging Layer\" Group=\"Collapsed\" Label=\"Staging Layer\"/>");
                    dgmlExtract.AppendLine("     <Node Id=\"Data Vault\" Group=\"Expanded\" Label=\"Data Vault\"/>");
                    #endregion

                    #region Subject Area nodes
                    // Add the subject area nodes
                    dgmlExtract.AppendLine("     <!-- Subject Area nodes -->");
                    var sqlStatementForSubjectAreas = new StringBuilder();
                    try
                    {
                        sqlStatementForSubjectAreas.AppendLine("SELECT DISTINCT SUBJECT_AREA");
                        sqlStatementForSubjectAreas.AppendLine("FROM [interface].[INTERFACE_SUBJECT_AREA]");

                        var modelRelationshipsLinksDataTable = Utility.GetDataTable(ref connOmd, sqlStatementForSubjectAreas.ToString());

                        foreach (DataRow row in modelRelationshipsLinksDataTable.Rows)
                        {
                            //dgmlExtract.AppendLine("     <Link Source=\"" + (string)row["BUSINESS_CONCEPT"] + "\" Target=\"" + (string)row["CONTEXT_TABLE"] + "\" />");
                            dgmlExtract.AppendLine("     <Node Id=\"SubjectArea_" + (string)row["SUBJECT_AREA"] + "\"  Group=\"Collapsed\" Category=\"Subject Area\" Label=\"" + (string)row["SUBJECT_AREA"] + "\" />");
                            edgeBuilder.AppendLine("     <Link Source=\"Data Vault\" Target=\"SubjectArea_" + (string)row["SUBJECT_AREA"] + "\" Category=\"Contains\" />");
                        }
                    }
                    catch (Exception)
                    {
                        errorCounter++;
                        errorLog.AppendLine("The following query caused an issue when generating the DGML file: " + sqlStatementForSubjectAreas);
                    }
                    #endregion

                    dgmlExtract.AppendLine("  </Nodes>");
                    //End of Nodes


                    //Edges and containers
                    dgmlExtract.AppendLine("  <Links>");
                    dgmlExtract.AppendLine("     <!-- Place regular nodes in layer containers ('contains') -->");
                    dgmlExtract.Append(edgeBuilder); // Add the containers (e.g. STG and PSA to Staging Layer, Hubs, Links and Satellites to Data Vault


                    // Separate routine to create table / attribute relationships
                    dgmlExtract.AppendLine("     <!-- Table / Attribute relationships -->");
                    foreach (DataRow row in satelliteAttributes.Rows)
                    {
                        var sourceNodeSat = (string) row["TARGET_NAME"];
                        var targetNodeSat = "dwh_" + (string) row["TARGET_ATTRIBUTE_NAME"];
                        var sourceNodeStg = (string) row["SOURCE_NAME"];
                        var targetNodeStg = "staging_" + (string) row["SOURCE_ATTRIBUTE_NAME"];

                        // This is adding the attributes to the tables
                        dgmlExtract.AppendLine("     <Link Source=\"" + sourceNodeSat + "\" Target=\"" +targetNodeSat + "\" Category=\"Contains\" />");
                        dgmlExtract.AppendLine("     <Link Source=\"" + sourceNodeStg + "\" Target=\"" +targetNodeStg + "\" Category=\"Contains\" />");

                        // This is adding the edge between the attributes
                        dgmlExtract.AppendLine("     <Link Source=\"" + targetNodeStg + "\" Target=\"" +targetNodeSat + "\" />");
                    }

                    // Get the source / target model relationships for Hubs and Satellites
                    List<string> segmentNodeList = new List<string>();
                    var modelRelationshipsHubDataTable = new DataTable();
                    var sqlStatementForHubCategories = new StringBuilder();
                    try
                    {

                        sqlStatementForHubCategories.AppendLine("SELECT *");
                        sqlStatementForHubCategories.AppendLine("FROM [interface].[INTERFACE_SOURCE_SATELLITE_XREF]");
                        sqlStatementForHubCategories.AppendLine("WHERE TARGET_TYPE = 'Normal'");

                        modelRelationshipsHubDataTable = Utility.GetDataTable(ref connOmd, sqlStatementForHubCategories.ToString());
                    }
                    catch
                    {
                        errorCounter++;
                        errorLog.AppendLine("The following query caused an issue when generating the DGML file: " + sqlStatementForHubCategories);
                    }

                    foreach (DataRow row in modelRelationshipsHubDataTable.Rows)
                    {
                        var modelRelationshipsHub = (string)row["TARGET_NAME"];

                        if (!segmentNodeList.Contains(modelRelationshipsHub))
                        {
                            segmentNodeList.Add(modelRelationshipsHub);
                        }
                    }


                    //Add the relationships between core business concepts - from Hub to Link
                    dgmlExtract.AppendLine("     <!-- Hub / Link relationships -->");
                    var sqlStatementForRelationships = new StringBuilder();
                    try
                    {
                        sqlStatementForRelationships.AppendLine("SELECT DISTINCT [HUB_NAME], [TARGET_NAME]");
                        sqlStatementForRelationships.AppendLine("FROM [interface].[INTERFACE_HUB_LINK_XREF]");
                        sqlStatementForRelationships.AppendLine("WHERE HUB_NAME NOT IN ('N/A')");

                        var businessConceptsRelationships = Utility.GetDataTable(ref connOmd, sqlStatementForRelationships.ToString());

                        foreach (DataRow row in businessConceptsRelationships.Rows)
                        {
                            dgmlExtract.AppendLine("     <Link Source=\"" + (string)row["HUB_NAME"] + "\" Target=\"" + (string)row["TARGET_NAME"] + "\" />");
                        }
                    }
                    catch
                    {
                        errorCounter++;
                        errorLog.AppendLine("The following query caused an issue when generating the DGML file: " + sqlStatementForRelationships);
                    }


                    // Add the relationships to the context tables
                    dgmlExtract.AppendLine("     <!-- Relationships between Hubs/Links to context and their subject area -->");
                    var sqlStatementForLinkCategories = new StringBuilder();
                    try
                    {
                        sqlStatementForLinkCategories.AppendLine("SELECT *");
                        sqlStatementForLinkCategories.AppendLine("FROM [interface].[INTERFACE_SUBJECT_AREA]");

                        var modelRelationshipsLinksDataTable = Utility.GetDataTable(ref connOmd, sqlStatementForLinkCategories.ToString());

                        foreach (DataRow row in modelRelationshipsLinksDataTable.Rows)
                        {
                            var businessConcept = (string) row["BUSINESS_CONCEPT"];

                            var contextTable = Utility.ConvertFromDBVal<string>(row["CONTEXT_TABLE"]);

                            dgmlExtract.AppendLine("     <Link Source=\"" + businessConcept + "\" Target=\"" + contextTable + "\" />");

                            dgmlExtract.AppendLine("     <Link Source=\"SubjectArea_" + (string)row["SUBJECT_AREA"] + "\" Target=\"" + businessConcept + "\" Category=\"Contains\" />");

                            if (contextTable != null)
                            {
                                dgmlExtract.AppendLine("     <Link Source=\"SubjectArea_" + (string) row["SUBJECT_AREA"] + "\" Target=\"" + contextTable + "\" Category=\"Contains\" />");
                            }
                        }

                    }
                    catch (Exception)
                    {
                        errorCounter++;
                        errorLog.AppendLine("The following query caused an issue when generating the DGML file: " + sqlStatementForLinkCategories);
                    }


                    // Add the regular source-to-target mappings as edges using the datagrid
                    dgmlExtract.AppendLine("     <!-- Regular source-to-target mappings -->");
                    for (var i = 0; i < dataGridViewTableMetadata.Rows.Count - 1; i++)
                    {
                        var row = dataGridViewTableMetadata.Rows[i];
                        var sourceNode = row.Cells[(int)TableMappingMetadataColumns.SourceTable].Value.ToString();
                        var targetNode = row.Cells[(int)TableMappingMetadataColumns.TargetTable].Value.ToString();
                        var businessKey = row.Cells[(int)TableMappingMetadataColumns.BusinessKeyDefinition].Value.ToString();

                        dgmlExtract.AppendLine("     <Link Source=\"" + sourceNode + "\" Target=\"" + targetNode + "\" BusinessKeyDefinition=\"" + businessKey + "\"/>");
                    }

                    dgmlExtract.AppendLine("  </Links>");
                    // End of edges and containers


                    //Add categories
                    dgmlExtract.AppendLine("  <Categories>");
                    dgmlExtract.AppendLine("    <Category Id = \"Sources\" Label = \"Sources\" Background = \"#FFE51400\" IsTag = \"True\" /> ");
                    dgmlExtract.AppendLine("    <Category Id = \"Landing Area\" Label = \"Landing Area\" IsTag = \"True\" /> ");
                    dgmlExtract.AppendLine("    <Category Id = \"Persistent Staging Area\" Label = \"Persistent Staging Area\" IsTag = \"True\" /> ");
                    dgmlExtract.AppendLine("    <Category Id = \"Hub\" Label = \"Hub\" IsTag = \"True\" /> ");
                    dgmlExtract.AppendLine("    <Category Id = \"Link\" Label = \"Link\" IsTag = \"True\" /> ");
                    dgmlExtract.AppendLine("    <Category Id = \"Satellite\" Label = \"Satellite\" IsTag = \"True\" /> ");
                    dgmlExtract.AppendLine("    <Category Id = \"Subject Area\" Label = \"Subject Area\" IsTag = \"True\" /> ");
                    dgmlExtract.AppendLine("  </Categories>");

                    //Add category styles 
                    dgmlExtract.AppendLine("  <Styles >");

                    dgmlExtract.AppendLine("    <Style TargetType = \"Node\" GroupLabel = \"Sources\" ValueLabel = \"Has category\" >");
                    dgmlExtract.AppendLine("      <Condition Expression = \"HasCategory('Sources')\" />");
                    dgmlExtract.AppendLine("      <Setter Property=\"Foreground\" Value=\"#FF000000\" />");
                    dgmlExtract.AppendLine("      <Setter Property = \"Background\" Value = \"#FFFFFFFF\" />");
                    dgmlExtract.AppendLine("      <Setter Property = \"Icon\" Value = \"pack://application:,,,/Microsoft.VisualStudio.Progression.GraphControl;component/Icons/Table.png\" />");
                    dgmlExtract.AppendLine("    </Style >");

                    dgmlExtract.AppendLine("    <Style TargetType = \"Node\" GroupLabel = \"Landing Area\" ValueLabel = \"Has category\" >");
                    dgmlExtract.AppendLine("      <Condition Expression = \"HasCategory('Landing Area')\" />");
                    dgmlExtract.AppendLine("      <Setter Property=\"Foreground\" Value=\"#FE000000\" />");
                    dgmlExtract.AppendLine("      <Setter Property = \"Background\" Value = \"#FE6E6A69\" />");
                    dgmlExtract.AppendLine("      <Setter Property = \"Icon\" Value = \"pack://application:,,,/Microsoft.VisualStudio.Progression.GraphControl;component/Icons/Table.png\" />");
                    dgmlExtract.AppendLine("    </Style >");

                    dgmlExtract.AppendLine("    <Style TargetType = \"Node\" GroupLabel = \"Persistent Staging Area\" ValueLabel = \"Has category\" >");
                    dgmlExtract.AppendLine("      <Condition Expression = \"HasCategory('Persistent Staging Area')\" />");
                    dgmlExtract.AppendLine("      <Setter Property=\"Foreground\" Value=\"#FA000000\" />");
                    dgmlExtract.AppendLine("      <Setter Property = \"Background\" Value = \"#FA6E6A69\" />");
                    dgmlExtract.AppendLine("      <Setter Property = \"Icon\" Value = \"pack://application:,,,/Microsoft.VisualStudio.Progression.GraphControl;component/Icons/Table.png\" />");
                    dgmlExtract.AppendLine("    </Style >");

                    dgmlExtract.AppendLine("    <Style TargetType = \"Node\" GroupLabel = \"Hub\" ValueLabel = \"Has category\" >");
                    dgmlExtract.AppendLine("      <Condition Expression = \"HasCategory('Hub')\" />");
                    dgmlExtract.AppendLine("      <Setter Property=\"Foreground\" Value=\"#FF000000\" />");
                    dgmlExtract.AppendLine("      <Setter Property = \"Background\" Value = \"#FF6495ED\" />");
                    dgmlExtract.AppendLine("      <Setter Property = \"Icon\" Value = \"pack://application:,,,/Microsoft.VisualStudio.Progression.GraphControl;component/Icons/Table.png\" />");
                    dgmlExtract.AppendLine("    </Style >");

                    dgmlExtract.AppendLine("    <Style TargetType = \"Node\" GroupLabel = \"Link\" ValueLabel = \"Has category\" >");
                    dgmlExtract.AppendLine("      <Condition Expression = \"HasCategory('Link')\" />");
                    dgmlExtract.AppendLine("      <Setter Property=\"Foreground\" Value=\"#FF000000\" />");
                    dgmlExtract.AppendLine("      <Setter Property = \"Background\" Value = \"#FFB22222\" />");
                    dgmlExtract.AppendLine("      <Setter Property = \"Icon\" Value = \"pack://application:,,,/Microsoft.VisualStudio.Progression.GraphControl;component/Icons/Table.png\" />");
                    dgmlExtract.AppendLine("    </Style >");

                    dgmlExtract.AppendLine("    <Style TargetType = \"Node\" GroupLabel = \"Satellite\" ValueLabel = \"Has category\" >");
                    dgmlExtract.AppendLine("      <Condition Expression = \"HasCategory('Satellite')\" />");
                    dgmlExtract.AppendLine("      <Setter Property=\"Foreground\" Value=\"#FF000000\" />");
                    dgmlExtract.AppendLine("      <Setter Property = \"Background\" Value = \"#FFC0A000\" />");
                    dgmlExtract.AppendLine("      <Setter Property = \"Icon\" Value = \"pack://application:,,,/Microsoft.VisualStudio.Progression.GraphControl;component/Icons/Table.png\" />");
                    dgmlExtract.AppendLine("    </Style >");

                    dgmlExtract.AppendLine("    <Style TargetType = \"Node\" GroupLabel = \"Subject Area\" ValueLabel = \"Has category\" >");
                    dgmlExtract.AppendLine("      <Condition Expression = \"HasCategory('Subject Area')\" />");
                    dgmlExtract.AppendLine("      <Setter Property=\"Foreground\" Value=\"#FF000000\" />");
                    dgmlExtract.AppendLine("      <Setter Property = \"Background\" Value = \"#FFFFFFFF\" />");
                    dgmlExtract.AppendLine("      <Setter Property = \"Icon\" Value = \"pack://application:,,,/Microsoft.VisualStudio.Progression.GraphControl;component/Icons/Table.png\" />");
                    dgmlExtract.AppendLine("    </Style >");

                    dgmlExtract.AppendLine("  </Styles >");

                    dgmlExtract.AppendLine("</DirectedGraph>");
                    // End of graph file creation


                    // Error handling
                    if (errorCounter > 0)
                    {
                        richTextBoxInformation.AppendText("\r\nWarning! There were " + errorCounter +
                                                          " error(s) found while generating the DGML file.\r\n");
                        richTextBoxInformation.AppendText("Please check the Error Log for details \r\n");
                        richTextBoxInformation.AppendText("\r\n");

                        using (var outfile =
                            new StreamWriter(GlobalParameters.ConfigurationPath + @"\Error_Log.txt"))
                        {
                            outfile.Write(errorLog.ToString());
                            outfile.Close();
                        }
                    }
                    else
                    {
                        richTextBoxInformation.AppendText("\r\nNo errors were detected.\r\n");
                    }


                    // Writing the output
                    using (StreamWriter outfile = new StreamWriter(chosenFile))
                    {
                        outfile.Write(dgmlExtract.ToString());
                        outfile.Close();
                    }

                    richTextBoxInformation.AppendText("The DGML metadata file file://" + chosenFile + " has been saved successfully.");
                }
                else
                {
                    richTextBoxInformation.AppendText("There was no metadata to create the graph with, is the grid view empty?");
                }
            }
        }

        private void textBoxFilterCriterion_OnDelayedTextChanged(object sender, EventArgs e)
        {
            foreach (DataGridViewRow dr in dataGridViewTableMetadata.Rows)
            {
                dr.Visible = true;
            }

            foreach (DataGridViewRow dr in dataGridViewTableMetadata.Rows)
            {
                if (dr.Cells[(int)TableMappingMetadataColumns.TargetTable].Value != null)
                {
                    if (!dr.Cells[(int)TableMappingMetadataColumns.TargetTable].Value.ToString().Contains(textBoxFilterCriterion.Text) && !dr.Cells[(int)TableMappingMetadataColumns.SourceTable].Value.ToString().Contains(textBoxFilterCriterion.Text))
                    {
                        CurrencyManager currencyManager1 = (CurrencyManager)BindingContext[dataGridViewTableMetadata.DataSource];
                        currencyManager1.SuspendBinding();
                        dr.Visible = false;
                        currencyManager1.ResumeBinding();
                    }
                }
            }
        }

        private void saveTableMappingAsJSONToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                var theDialog = new SaveFileDialog
                {
                    Title = @"Save Table Mapping Metadata File",
                    Filter = @"JSON files|*.json",
                    InitialDirectory =  GlobalParameters.ConfigurationPath //Application.StartupPath + @"\Configuration\"
                };

                var ret = STAShowDialog(theDialog);

                if (ret == DialogResult.OK)
                {
                    try
                    {
                        var chosenFile = theDialog.FileName;

                        DataTable gridDataTable = (DataTable)_bindingSourceTableMetadata.DataSource;

                        // Make sure the output is sorted
                        TableMapping.SetTableDataTableSorting();
                        
                        gridDataTable.TableName = "TableMappingMetadata";

                        JArray outputFileArray = new JArray();
                        foreach (DataRow singleRow in gridDataTable.DefaultView.ToTable().Rows)
                        {
                            JObject individualRow = JObject.FromObject(new
                            {
                                enabledIndicator = singleRow[(int)TableMappingMetadataColumns.Enabled].ToString(),
                                tableMappingHash = singleRow[(int)TableMappingMetadataColumns.HashKey].ToString(),
                                versionId = singleRow[(int)TableMappingMetadataColumns.VersionId].ToString(),
                                sourceTable = singleRow[(int)TableMappingMetadataColumns.SourceTable].ToString(),
                                sourceConnection = singleRow[(int)TableMappingMetadataColumns.SourceConnection].ToString(),
                                targetTable = singleRow[(int)TableMappingMetadataColumns.TargetTable].ToString(),
                                targetConnection = singleRow[(int)TableMappingMetadataColumns.TargetConnection].ToString(),
                                businessKeyDefinition = singleRow[(int)TableMappingMetadataColumns.BusinessKeyDefinition].ToString(),
                                drivingKeyDefinition = singleRow[(int)TableMappingMetadataColumns.DrivingKeyDefinition].ToString(),
                                filterCriteria = singleRow[(int)TableMappingMetadataColumns.FilterCriterion].ToString()
                            });
                            outputFileArray.Add(individualRow);
                        }

                        string json = JsonConvert.SerializeObject(outputFileArray, Formatting.Indented);

                        File.WriteAllText(chosenFile, json);

                        richTextBoxInformation.Text = "The Table Mapping metadata file " + chosenFile + " saved successfully.";
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.ToString());
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("A problem occured when attempting to save the file to disk. The detail error message is: " + ex.Message);
            }
        }

        /// <summary>
        /// Run the validation based on the validation settings (in the validation form / file)
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonValidation_Click(object sender, EventArgs e)
        {
            richTextBoxInformation.Clear();

            if (radioButtonPhysicalMode.Checked == false && _bindingSourcePhysicalModelMetadata.Count == 0)
            {
                richTextBoxInformation.Text += "There is no physical model metadata available, so the metadata can only be validated with the 'Ignore Version' enabled.\r\n ";
            }
            else
            {
                if (backgroundWorkerValidationOnly.IsBusy) return;
                // create a new instance of the alert form
                _alertValidation = new Form_Alert();
                // event handler for the Cancel button in AlertForm
                _alertValidation.Canceled += buttonCancel_Click;
                _alertValidation.Show();
                // Start the asynchronous operation.
                backgroundWorkerValidationOnly.RunWorkerAsync();
            }
        }

        private void openOutputDirectoryToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                System.Diagnostics.Process.Start(GlobalParameters.OutputPath);
            }
            catch (Exception ex)
            {
                richTextBoxInformation.Text = "An error has occured while attempting to open the output directory. The error message is: " + ex;
            }
        }


        /// <summary>
        ///   Method called when clicking the Reverse Engineer button
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ReverseEngineerMetadataButtonClick(object sender, EventArgs e)
        {
            richTextBoxInformation.Clear();
            richTextBoxInformation.Text += "Commencing reverse-engineering the model metadata from the database.\r\n";

            var completeDataTable = new DataTable();

            foreach (var item in checkedListBoxReverseEngineeringAreas.CheckedItems)
            {
                var localConnectionObject = (KeyValuePair<TeamConnection, string>)item;

                var localSqlConnection = new SqlConnection { ConnectionString = localConnectionObject.Key.CreateSqlServerConnectionString(false) };
                var reverseEngineerResults = ReverseEngineerModelMetadata(localSqlConnection, localConnectionObject.Key.DatabaseServer.DatabaseName);

                if (reverseEngineerResults != null)
                {
                    completeDataTable.Merge(reverseEngineerResults);
                }
            }

            DataTable distinctTable = completeDataTable.DefaultView.ToTable( /*distinct*/ true);

            distinctTable.DefaultView.Sort = "[DATABASE_NAME] ASC, [SCHEMA_NAME] ASC, [TABLE_NAME] ASC, [ORDINAL_POSITION] ASC";

            // Display the results on the datagrid
            _bindingSourcePhysicalModelMetadata.DataSource = distinctTable;

            // Set the column header names.
            dataGridViewPhysicalModelMetadata.DataSource = _bindingSourcePhysicalModelMetadata;
            dataGridViewPhysicalModelMetadata.ColumnHeadersVisible = true;
            dataGridViewPhysicalModelMetadata.Columns[0].Visible = false;
            dataGridViewPhysicalModelMetadata.Columns[1].Visible = false;

            dataGridViewPhysicalModelMetadata.Columns[0].HeaderText = "Hash Key"; //Key column
            dataGridViewPhysicalModelMetadata.Columns[1].HeaderText = "Version ID"; //Key column
            dataGridViewPhysicalModelMetadata.Columns[2].HeaderText = "Database Name"; //Key column
            dataGridViewPhysicalModelMetadata.Columns[3].HeaderText = "Schema Name"; //Key column
            dataGridViewPhysicalModelMetadata.Columns[4].HeaderText = "Table Name"; //Key column
            dataGridViewPhysicalModelMetadata.Columns[5].HeaderText = "Column Name"; //Key column
            dataGridViewPhysicalModelMetadata.Columns[6].HeaderText = "Data Type";
            dataGridViewPhysicalModelMetadata.Columns[7].HeaderText = "Length";
            dataGridViewPhysicalModelMetadata.Columns[8].HeaderText = "Precision";
            dataGridViewPhysicalModelMetadata.Columns[9].HeaderText = "Position";
            dataGridViewPhysicalModelMetadata.Columns[10].HeaderText = "Primary Key";
            dataGridViewPhysicalModelMetadata.Columns[11].HeaderText = "Multi-Active";

            foreach (DataRow row in completeDataTable.Rows) //Flag as new row so it's detected by the save button
            {
                row.SetAdded();
            }
        }


        /// <summary>
        ///   Connect to a given database and return the data dictionary (catalog) information in the datagrid.
        /// </summary>
        /// <param name="conn"></param>
        /// <param name="databaseName"></param>
        private DataTable ReverseEngineerModelMetadata(SqlConnection conn, string databaseName)
        {
            try
            {
                conn.Open();
            }
            catch (Exception exception)
            {
                richTextBoxInformation.Text += "An error has occurred uploading the model for the new version because the database could not be connected to. The error message is: " + exception.Message + ".\r\n";
            }

            // Get everything as local variables to reduce multi-threading issues
            var effectiveDateTimeAttribute = TeamConfigurationSettings.EnableAlternativeSatelliteLoadDateTimeAttribute == "True" ? TeamConfigurationSettings.AlternativeSatelliteLoadDateTimeAttribute : TeamConfigurationSettings.LoadDateTimeAttribute;
            var dwhKeyIdentifier = TeamConfigurationSettings.DwhKeyIdentifier; //Indicates _HSH, _SK etc.
            var keyIdentifierLocation = TeamConfigurationSettings.KeyNamingLocation;

            // Create the attribute selection statement for the array
            var sqlStatementForAttributeVersion = new StringBuilder();

            sqlStatementForAttributeVersion.AppendLine("SELECT ");
            sqlStatementForAttributeVersion.AppendLine("  CONVERT(CHAR(32),HASHBYTES('MD5',CONVERT(NVARCHAR(100), " + GlobalParameters.CurrentVersionId + ") + '|' + OBJECT_NAME(main.OBJECT_ID) + '|' + main.[name]),2) AS ROW_CHECKSUM,");
            sqlStatementForAttributeVersion.AppendLine("  " + GlobalParameters.CurrentVersionId + " AS [VERSION_ID],");
            sqlStatementForAttributeVersion.AppendLine("  DB_NAME(DB_ID('"+databaseName+"')) AS [DATABASE_NAME],");
            sqlStatementForAttributeVersion.AppendLine("  OBJECT_SCHEMA_NAME(main.OBJECT_ID) AS [SCHEMA_NAME],");
            sqlStatementForAttributeVersion.AppendLine("  OBJECT_NAME(main.OBJECT_ID) AS [TABLE_NAME], ");
            sqlStatementForAttributeVersion.AppendLine("  main.[name] AS [COLUMN_NAME], ");
            sqlStatementForAttributeVersion.AppendLine("  t.[name] AS [DATA_TYPE], ");
            sqlStatementForAttributeVersion.AppendLine("  CAST(COALESCE(");
            sqlStatementForAttributeVersion.AppendLine("    CASE WHEN UPPER(t.[name]) = 'NVARCHAR' THEN main.[max_length]/2"); //Exception for unicode
            sqlStatementForAttributeVersion.AppendLine("    ELSE main.[max_length]");
            sqlStatementForAttributeVersion.AppendLine("    END");
            sqlStatementForAttributeVersion.AppendLine("     ,0) AS VARCHAR(100)) AS [CHARACTER_MAXIMUM_LENGTH],");
            sqlStatementForAttributeVersion.AppendLine("  CAST(COALESCE(main.[precision],0) AS VARCHAR(100)) AS [NUMERIC_PRECISION], ");
            sqlStatementForAttributeVersion.AppendLine("  CAST(main.[column_id] AS VARCHAR(100)) AS [ORDINAL_POSITION], ");

            sqlStatementForAttributeVersion.AppendLine("  CASE ");
            sqlStatementForAttributeVersion.AppendLine("    WHEN keysub.COLUMN_NAME IS NULL ");
            sqlStatementForAttributeVersion.AppendLine("    THEN 'N' ");
            sqlStatementForAttributeVersion.AppendLine("    ELSE 'Y' ");
            sqlStatementForAttributeVersion.AppendLine("  END AS PRIMARY_KEY_INDICATOR, ");

            sqlStatementForAttributeVersion.AppendLine("  CASE ");
            sqlStatementForAttributeVersion.AppendLine("    WHEN ma.COLUMN_NAME IS NULL ");
            sqlStatementForAttributeVersion.AppendLine("    THEN 'N' ");
            sqlStatementForAttributeVersion.AppendLine("    ELSE 'Y' ");
            sqlStatementForAttributeVersion.AppendLine("  END AS MULTI_ACTIVE_INDICATOR ");

            sqlStatementForAttributeVersion.AppendLine("FROM [" + databaseName + "].sys.columns main");
            sqlStatementForAttributeVersion.AppendLine("JOIN sys.types t ON main.user_type_id=t.user_type_id");
            sqlStatementForAttributeVersion.AppendLine("-- Primary Key");
            sqlStatementForAttributeVersion.AppendLine("LEFT OUTER JOIN (");
            sqlStatementForAttributeVersion.AppendLine("	SELECT ");
            sqlStatementForAttributeVersion.AppendLine("	  sc.name AS TABLE_NAME,");
            sqlStatementForAttributeVersion.AppendLine("	  C.name AS COLUMN_NAME");
            sqlStatementForAttributeVersion.AppendLine("	FROM [" + databaseName + "].sys.index_columns A");
            sqlStatementForAttributeVersion.AppendLine("	JOIN [" + databaseName + "].sys.indexes B");
            sqlStatementForAttributeVersion.AppendLine("	ON A.OBJECT_ID=B.OBJECT_ID AND A.index_id=B.index_id");
            sqlStatementForAttributeVersion.AppendLine("	JOIN [" + databaseName + "].sys.columns C");
            sqlStatementForAttributeVersion.AppendLine("	ON A.column_id=C.column_id AND A.OBJECT_ID=C.OBJECT_ID");
            sqlStatementForAttributeVersion.AppendLine("	JOIN [" + databaseName + "].sys.tables sc on sc.OBJECT_ID = A.OBJECT_ID");
            sqlStatementForAttributeVersion.AppendLine("	WHERE is_primary_key=1 ");
            sqlStatementForAttributeVersion.AppendLine(") keysub");
            sqlStatementForAttributeVersion.AppendLine("   ON OBJECT_NAME(main.OBJECT_ID) = keysub.TABLE_NAME");
            sqlStatementForAttributeVersion.AppendLine("  AND main.[name] = keysub.COLUMN_NAME");

            //Multi-active
            sqlStatementForAttributeVersion.AppendLine("-- Multi-Active");
            sqlStatementForAttributeVersion.AppendLine("LEFT OUTER JOIN (");
            sqlStatementForAttributeVersion.AppendLine("	SELECT ");
            sqlStatementForAttributeVersion.AppendLine("		sc.name AS TABLE_NAME,");
            sqlStatementForAttributeVersion.AppendLine("		C.name AS COLUMN_NAME");
            sqlStatementForAttributeVersion.AppendLine("	FROM [" + databaseName + "].sys.index_columns A");
            sqlStatementForAttributeVersion.AppendLine("	JOIN [" + databaseName + "].sys.indexes B");
            sqlStatementForAttributeVersion.AppendLine("	ON A.OBJECT_ID=B.OBJECT_ID AND A.index_id=B.index_id");
            sqlStatementForAttributeVersion.AppendLine("	JOIN [" + databaseName + "].sys.columns C");
            sqlStatementForAttributeVersion.AppendLine("	ON A.column_id=C.column_id AND A.OBJECT_ID=C.OBJECT_ID");
            sqlStatementForAttributeVersion.AppendLine("	JOIN [" + databaseName + "].sys.tables sc on sc.OBJECT_ID = A.OBJECT_ID");
            sqlStatementForAttributeVersion.AppendLine("	WHERE is_primary_key=1");
            sqlStatementForAttributeVersion.AppendLine("	AND C.name NOT IN ('" + effectiveDateTimeAttribute + "')");

            if (keyIdentifierLocation == "Prefix")
            {
                sqlStatementForAttributeVersion.AppendLine("	AND C.name NOT LIKE '" + dwhKeyIdentifier + "_%'");
            }
            else
            {
                sqlStatementForAttributeVersion.AppendLine("	AND C.name NOT LIKE '%_" + dwhKeyIdentifier + "'");
            }

            sqlStatementForAttributeVersion.AppendLine("	) ma");
            sqlStatementForAttributeVersion.AppendLine("	ON OBJECT_NAME(main.OBJECT_ID) = ma.TABLE_NAME");
            sqlStatementForAttributeVersion.AppendLine("	AND main.[name] = ma.COLUMN_NAME");


            //sqlStatementForAttributeVersion.AppendLine("WHERE OBJECT_NAME(main.OBJECT_ID) LIKE '" + prefix + "_%'");
            sqlStatementForAttributeVersion.AppendLine("WHERE 1=1");

            // Retrieve (and apply) the list of tables to filter from the Table Mapping datagrid
            sqlStatementForAttributeVersion.AppendLine("  AND (");


            var filterList = new List<string>();
            foreach (DataRow row in ((DataTable)_bindingSourceTableMetadata.DataSource).Rows)
            {
                if (!filterList.Contains((string)row[TableMappingMetadataColumns.SourceTable.ToString()]))
                {
                    filterList.Add((string)row[TableMappingMetadataColumns.SourceTable.ToString()]);
                }

                if (!filterList.Contains((string)row[TableMappingMetadataColumns.TargetTable.ToString()]))
                {
                    filterList.Add((string)row[TableMappingMetadataColumns.TargetTable.ToString()]);
                }
            }

            foreach (var filter in filterList)
            {
                var fullyQualifiedName = MetadataHandling.GetSchema(filter).FirstOrDefault();
                // Always add the 'regular' mapping.
                sqlStatementForAttributeVersion.AppendLine("  (OBJECT_NAME(main.OBJECT_ID) = '"+ fullyQualifiedName.Value+ "' AND OBJECT_SCHEMA_NAME(main.OBJECT_ID) = '"+fullyQualifiedName.Key+"')");
                sqlStatementForAttributeVersion.AppendLine("  OR");

                //// Workaround to allow PSA tables to be reverse-engineered automatically by replacing the STG prefix/suffix
                //if (filter.StartsWith(TeamConfigurationSettings.StgTablePrefixValue+"_") || filter.EndsWith("_"+TeamConfigurationSettings.StgTablePrefixValue))
                //{
                //    var tempFilter = filter.Replace(TeamConfigurationSettings.StgTablePrefixValue,TeamConfigurationSettings.PsaTablePrefixValue);
                //    sqlStatementForAttributeVersion.AppendLine("  '" + tempFilter + "',");
                //}
            }
            sqlStatementForAttributeVersion.Remove(sqlStatementForAttributeVersion.Length - 6, 6);
            sqlStatementForAttributeVersion.AppendLine();
            sqlStatementForAttributeVersion.AppendLine("  )");
            sqlStatementForAttributeVersion.AppendLine("ORDER BY main.column_id");

            var reverseEngineerResults = Utility.GetDataTable(ref conn, sqlStatementForAttributeVersion.ToString());
            conn.Close();
            return reverseEngineerResults;
        }

        #region ContextMenu
        private void dataGridViewTableMetadata_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                var hti = dataGridViewTableMetadata.HitTest(e.X, e.Y);

                // If the column in the selected cell is a Combobox and there are multiple cells selected then c\setup a separate context menu
                //var currentCell = dataGridViewTableMetadata.Rows[hti.RowIndex].Cells[hti.ColumnIndex];
                //var currentColumn = dataGridViewTableMetadata.Columns[hti.ColumnIndex];

                //if (currentColumn.GetType() == typeof(DataGridViewComboBoxColumn))
                //{
                //    var selectedCellCollection = dataGridViewTableMetadata.SelectedCells;


                //    //if (currentCell.Value != DBNull.Value)
                //    //{
                //    //    string currentValue = (string) currentCell.Value;
                //    //}
                //}
                //else
                //{
                    // Normal selection
                    dataGridViewTableMetadata.ClearSelection();
                    dataGridViewTableMetadata.Rows[hti.RowIndex].Selected = true;
                //}
            }
            //if (e.Button == MouseButtons.Left)
            //{
            //    var hti = dataGridViewTableMetadata.HitTest(e.X, e.Y);
            //    var currentCell = dataGridViewTableMetadata.Rows[hti.RowIndex].Cells[hti.ColumnIndex];

            //    if (currentCell.GetType() == typeof(DataGridViewComboBoxCell))
            //    {
                   
            //       if (currentCell.Value != DBNull.Value)
            //       {
            //           string currentValue = (string)currentCell.Value;
            //        }
            //    }
            //}
        }

        private void dataGridViewAttributeMetadata_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                var hti = dataGridViewAttributeMetadata.HitTest(e.X, e.Y);
                dataGridViewAttributeMetadata.ClearSelection();
                dataGridViewAttributeMetadata.Rows[hti.RowIndex].Selected = true;
            }
        }

        private void dataGridViewModelMetadata_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                var hti = dataGridViewPhysicalModelMetadata.HitTest(e.X, e.Y);
                dataGridViewPhysicalModelMetadata.ClearSelection();
                dataGridViewPhysicalModelMetadata.Rows[hti.RowIndex].Selected = true;
            }
        }

        /// <summary>
        /// This method is called from the context menu on the data grid. It exports the selected row to Json.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void ExportThisRowAsSourceToTargetInterfaceJSONToolStripMenuItem_Click(object sender, EventArgs e)
        {
            richTextBoxInformation.Clear();

            // Check if any cells were clicked / selected.
            Int32 selectedRow = dataGridViewTableMetadata.Rows.GetFirstRow(DataGridViewElementStates.Selected);
           
            List<DataGridViewRow> generationMetadataList = new List<DataGridViewRow>();

            DataGridViewRow generationMetadataRow = dataGridViewTableMetadata.Rows[selectedRow];
            generationMetadataList.Add(generationMetadataRow);
            // Generate the result
            GenerateFromPattern(generationMetadataList);
        }

        /// <summary>
        /// This method is called from the context menu on the data grid. It deletes the row from the grid.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void deleteThisRowFromTableDataGridToolStripMenuItem_Click(object sender, EventArgs e)
        {
            

            var selectedRows = dataGridViewTableMetadata.SelectedRows;

            foreach (DataGridViewRow bla in selectedRows)
            {
                if (bla.IsNewRow)
                {

                }
                else
                {
                    Int32 rowToDelete = dataGridViewTableMetadata.Rows.GetFirstRow(DataGridViewElementStates.Selected);
                    dataGridViewTableMetadata.Rows.RemoveAt(rowToDelete);
                }
            }


        }
        #endregion

        /// <summary>
        ///   Run the validation checks
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void backgroundWorkerValidation_DoWork(object sender, DoWorkEventArgs e)
        {
            BackgroundWorker worker = sender as BackgroundWorker;

            // Handling multi-threading
            if (worker != null && worker.CancellationPending)
            {
                e.Cancel = true;
            }
            else
            {
               
                _alertValidation.SetTextLogging("Commencing validation on available metadata according to settings in in the validation screen.\r\n\r\n");
                MetadataParameters.ValidationIssues = 0;
                if (ValidationSettings.SourceObjectExistence == "True")
                {
                    ValidateObjectExistence("source");
                }

                if (worker != null) worker.ReportProgress(15);

                if (ValidationSettings.TargetObjectExistence == "True")
                {
                    ValidateObjectExistence("target");
                }

                if (worker != null) worker.ReportProgress(30);

                if (ValidationSettings.SourceBusinessKeyExistence == "True")
                {
                    ValidateBusinessKeyObject();
                }

                if (ValidationSettings.SourceAttributeExistence == "True")
                {
                    ValidateAttributeExistence("source");
                }

                if (ValidationSettings.TargetAttributeExistence == "True")
                {
                    ValidateAttributeExistence("target");
                }

                if (worker != null) worker.ReportProgress(60);

                if (ValidationSettings.LogicalGroup == "True")
                {
                    ValidateLogicalGroup();
                }

                if (worker != null) worker.ReportProgress(75);

                if (ValidationSettings.LinkKeyOrder == "True")
                {
                    ValidateLinkKeyOrder();
                }

                if (worker != null) worker.ReportProgress(100);

                // Informing the user.
                _alertValidation.SetTextLogging("\r\nIn total " + MetadataParameters.ValidationIssues + " validation issues have been found.");
            }
        }

        internal static class MetadataParameters
        {
            // TEAM core path parameters
            public static int ValidationIssues { get; set; }
            public static bool ValidationRunning {get; set;}
        }

        /// <summary>
        /// This method runs a check against the Attribute Mappings DataGrid to assert if model metadata is available for the attributes. The attribute needs to exist somewhere, either in the physical model or in the model metadata in order for activation to run successfully.
        /// </summary>
        /// <param name="area"></param>
        private void ValidateAttributeExistence(string area)
        {
            string evaluationMode = radioButtonPhysicalMode.Checked ? "physical" : "virtual";

            // Map the area to the column in the datagrid (e.g. source or target)
            int areaColumnIndex = 0;
            int areaAttributeColumnIndex = 0;

            var localTableMappingConnectionDictionary = GetTableMappingConnections();

            switch (area)
            {
                case "source":
                    areaColumnIndex = 2;
                    areaAttributeColumnIndex = 3;
                    break;
                case "target":
                    areaColumnIndex = 4;
                    areaAttributeColumnIndex = 5;
                    break;
                default:
                    areaColumnIndex = 0;
                    areaAttributeColumnIndex = 0;
                    break;
            }

            // Informing the user.
            _alertValidation.SetTextLogging($"--> Commencing the validation to determine if the attributes in the {area} metadata exists in the model.\r\n");

            var resultList = new Dictionary<string, string>();
       

            foreach (DataGridViewRow row in dataGridViewAttributeMetadata.Rows)
            {
                if (!row.IsNewRow)
                {
                    string objectValidated;
                    var validationObject = row.Cells[areaColumnIndex].Value.ToString();
                    var validationAttribute = row.Cells[areaAttributeColumnIndex].Value.ToString();

                    if (evaluationMode == "physical" && MetadataHandling.GetTableType(validationObject, "", TeamConfigurationSettings).ToString() != MetadataHandling.TableTypes.Source.ToString()) // No need to evaluate the operational system (real sources)
                    {
                        
                        if (!localTableMappingConnectionDictionary.TryGetValue(validationObject, out var connectionValue))
                        {
                            // the key isn't in the dictionary.
                            GlobalParameters.TeamEventLog.Add(Event.CreateNewEvent(EventTypes.Warning,
                                $"The connection string for {validationObject} could not be derived. This occured during the validation of the attribute metadata. Possibly there is no existing Source Data Object to Target Data Object mapping in the grid."));

                            //MessageBox.Show("The connection string for " + validationObject + " could not be derived.");
                            return;
                        }

                        objectValidated = ClassMetadataValidation.ValidateAttributeExistencePhysical(validationObject, validationAttribute, connectionValue);
                    }
                    else if (evaluationMode == "virtual")
                    {
                        objectValidated = "";
                        // Exclude a lookup to the source
                        if (MetadataHandling.GetTableType(validationObject, "", TeamConfigurationSettings).ToString() != MetadataHandling.TableTypes.Source.ToString())
                        {
                            objectValidated = ClassMetadataValidation.ValidateAttributeExistenceVirtual(validationObject, validationAttribute, (DataTable)_bindingSourcePhysicalModelMetadata.DataSource);
                        }
                    }
                    else
                    {
                        objectValidated = "     The validation approach (physical/virtual) could not be asserted.";
                    }

                    // Add negative results to dictionary
                    if (objectValidated == "False" && !resultList.ContainsKey((validationAttribute)))
                    {
                        resultList.Add(validationAttribute, objectValidated); // Add objects that did not pass the test
                    }
                }
            }
            // Return the results back to the user
            if (resultList.Count > 0)
            {
                foreach (var objectValidationResult in resultList)
                {
                    _alertValidation.SetTextLogging("     " + objectValidationResult.Key +
                                                    " is tested with this outcome: " + objectValidationResult.Value +
                                                    "\r\n");
                }

                MetadataParameters.ValidationIssues = MetadataParameters.ValidationIssues + resultList.Count;

                _alertValidation.SetTextLogging("\r\n");
            }
            else
            {
                _alertValidation.SetTextLogging($"     There were no validation issues related to the existence of the {area} attribute.\r\n\r\n");
            }
        }

        /// <summary>
        /// Create a dictionary of all tables in the table mapping metadata grid and their connection strings.
        /// </summary>
        /// <returns></returns>
        public Dictionary<string, string> GetTableMappingConnections()
        {
            Dictionary<string, string> returnDictionary = new Dictionary<string, string>();

            // Create the dictionary of the connection keys and their connection strings.
            var localConnectionDictionary = LocalConnectionDictionary.GetLocalConnectionDictionary(TeamConfigurationSettings.ConnectionDictionary);

            foreach (DataGridViewRow row in dataGridViewTableMetadata.Rows)
            {
                if (row.IsNewRow == false)
                {
                    var sourceDataObject = row.Cells[TableMappingMetadataColumns.SourceTable.ToString()].Value.ToString();
                    var sourceDataObjectConnectionId = row.Cells[TableMappingMetadataColumns.SourceConnection.ToString()].Value.ToString();

                    var targetDataObject = row.Cells[TableMappingMetadataColumns.TargetTable.ToString()].Value.ToString();
                    var targetDataObjectConnectionId = row.Cells[TableMappingMetadataColumns.TargetConnection.ToString()].Value.ToString();

                    if (localConnectionDictionary.TryGetValue(sourceDataObjectConnectionId, out var sourceConnectionValue))
                    {
                        returnDictionary[sourceDataObject] = sourceConnectionValue;
                        //returnDictionary.Add(sourceDataObject, sourceConnectionValue);
                    }

                    if (localConnectionDictionary.TryGetValue(targetDataObjectConnectionId, out var targetConnectionValue))
                    {
                        returnDictionary[targetDataObject] = targetConnectionValue;
                        //returnDictionary.Add(targetDataObject, targetConnectionValue);
                    }
                }
            }

            return returnDictionary;
        }

        /// <summary>
        /// Create a dictionary of all target data objects and whether they are enabled in metadata or not (bool).
        /// </summary>
        /// <returns></returns>
        public Dictionary<string, bool> GetEnabledForDataObject()
        {
            Dictionary<string, bool> returnDictionary = new Dictionary<string, bool>();

            foreach (DataGridViewRow row in dataGridViewTableMetadata.Rows)
            {
                if (row.IsNewRow == false)
                {
                    string targetDataObject = row.Cells[TableMappingMetadataColumns.TargetTable.ToString()].Value.ToString();
                    bool rowEnabled = (bool)row.Cells[TableMappingMetadataColumns.Enabled.ToString()].Value;

                    if (rowEnabled)
                    {
                        returnDictionary[targetDataObject] = rowEnabled;
                    }
                }
            }

            return returnDictionary;
        }



        /// <summary>
        /// This method runs a check against the DataGrid to assert if model metadata is available for the object. The object needs to exist somewhere, either in the physical model or in the model metadata in order for activation to run succesfully.
        /// </summary>
        /// <param name="area"></param>
        private void ValidateObjectExistence(string area)
        {
            string evaluationMode = radioButtonPhysicalMode.Checked ? "physical" : "virtual";

            var localConnectionDictionary = LocalConnectionDictionary.GetLocalConnectionDictionary(TeamConfigurationSettings.ConnectionDictionary);

            // Map the area to the column in the datagrid (e.g. source or target)
            int areaColumnIndex = 0;
            int connectionColumnIndex = 0;
            switch (area)
            {
                case "source":
                    areaColumnIndex = (int)TableMappingMetadataColumns.SourceTable;
                    connectionColumnIndex = (int)TableMappingMetadataColumns.SourceConnection;
                    break;
                case "target":
                    areaColumnIndex = (int)TableMappingMetadataColumns.TargetTable;
                    connectionColumnIndex = (int)TableMappingMetadataColumns.TargetConnection;
                    break;
                default:
                    // Do nothing
                    break;
            }

            // Informing the user.
            _alertValidation.SetTextLogging($"--> Commencing the validation to determine if the objects in the {area} metadata exists in the model.\r\n");

            var resultList = new Dictionary<string, string>();

            foreach (DataGridViewRow row in dataGridViewTableMetadata.Rows)
            {
                if (!row.IsNewRow && (bool)row.Cells[TableMappingMetadataColumns.Enabled.ToString()].Value==true)
                {
                    string objectValidated;
                    var validationObject = row.Cells[areaColumnIndex].Value.ToString();
                    var connectionObject = row.Cells[connectionColumnIndex].Value.ToString();

                    if (evaluationMode == "physical" && MetadataHandling.GetTableType(validationObject, "", TeamConfigurationSettings).ToString() != MetadataHandling.TableTypes.Source.ToString()) // No need to evaluate the operational system (real sources)
                    {

                        if (!localConnectionDictionary.TryGetValue(connectionObject, out var connectionValue))
                        {
                            GlobalParameters.TeamEventLog.Add(Event.CreateNewEvent(EventTypes.Warning,
                                $"The connection string for {validationObject} could not be derived. This occured during the validation of the Data Object metadata (does the object exist in the database?). Possibly there is no connection assigned to the Data Object in the grid."));

                            // the key isn't in the dictionary.
                            //MessageBox.Show("The connection string for " + connectionObject + " could not be derived.");
                            
                            return;
                        }

                        try
                        {
                            objectValidated = ClassMetadataValidation.ValidateObjectExistencePhysical(validationObject, connectionValue);
                        }
                        catch
                        {
                            objectValidated = "     An issue occurred connecting to the database.";
                        }
                    }
                    else if (evaluationMode == "virtual")
                    {
                        objectValidated = "";
                        // Exclude a lookup to the source
                        if (MetadataHandling.GetTableType(validationObject,"", TeamConfigurationSettings).ToString() != MetadataHandling.TableTypes.Source.ToString())
                        {
                            objectValidated = ClassMetadataValidation.ValidateObjectExistenceVirtual(validationObject,
                                (DataTable) _bindingSourcePhysicalModelMetadata.DataSource);
                        }
                    }
                    else
                    {
                        objectValidated = "     The validation approach (physical/virtual) could not be asserted.";
                    }

                    // Add negative results to dictionary
                    if (objectValidated == "False" && !resultList.ContainsKey(validationObject))
                    {
                        resultList.Add(validationObject, objectValidated); // Add objects that did not pass the test
                    }
                }
            }

            // Return the results back to the user
            if (resultList.Count > 0)
            {
                foreach (var objectValidationResult in resultList)
                {
                    _alertValidation.SetTextLogging("     " + objectValidationResult.Key + " is tested with this outcome: " + objectValidationResult.Value + "\r\n");
                }

                MetadataParameters.ValidationIssues = MetadataParameters.ValidationIssues + resultList.Count;
                _alertValidation.SetTextLogging("\r\n");
            }
            else
            {
                _alertValidation.SetTextLogging($"     There were no validation issues related to the existence of the {area} table / object.\r\n\r\n");
            }

        }
 
        /// <summary>
        /// This method will check if the order of the keys in the Link is consistent with the physical table structures.
        /// </summary>
        internal void ValidateLinkKeyOrder()
        {
            string evaluationMode = radioButtonPhysicalMode.Checked ? "physical" : "virtual";

            #region Retrieving the Links
            // Informing the user.
            _alertValidation.SetTextLogging("--> Commencing the validation to ensure the order of Business Keys in the Link metadata corresponds with the physical model.\r\n");

            var localConnectionDictionary =
                LocalConnectionDictionary.GetLocalConnectionDictionary(TeamConfigurationSettings.ConnectionDictionary);


            // Creating a list of unique Link business key combinations from the data grid / data table
            var objectList = new List<Tuple<string, string, string, string>>();
            foreach (DataGridViewRow row in dataGridViewTableMetadata.Rows)
            {
                if (!row.IsNewRow && row.Cells[(int)TableMappingMetadataColumns.TargetTable].Value.ToString().StartsWith(TeamConfigurationSettings.LinkTablePrefixValue)) // Only select the lines that relate to a Link target
                {
                    // Derive the business key.
                    var businessKey = row.Cells[(int)TableMappingMetadataColumns.BusinessKeyDefinition].Value.ToString().Replace("''''", "'");

                    // Derive the connection
                    localConnectionDictionary.TryGetValue(row.Cells[(int) TableMappingMetadataColumns.TargetConnection].Value.ToString(), out var connectionValue);

                    var newValidationObject = new Tuple<string, string, string, string>
                        (
                        row.Cells[(int)TableMappingMetadataColumns.SourceTable].Value.ToString(),
                        row.Cells[(int)TableMappingMetadataColumns.TargetTable].Value.ToString(),
                        businessKey,
                        connectionValue
                        );
                    
                    if (!objectList.Contains(newValidationObject))
                    {
                        objectList.Add(newValidationObject);
                    }
                }
            } 

            // Execute the validation check using the list of unique objects
            var resultList = new Dictionary<string, bool>();

            foreach (var sourceObject in objectList)
            {
                // The validation check returns a Dictionary
                var sourceObjectValidated = ClassMetadataValidation.ValidateLinkKeyOrder
                (
                    sourceObject,
                    (DataTable)_bindingSourceTableMetadata.DataSource,
                    (DataTable)_bindingSourcePhysicalModelMetadata.DataSource,
                    evaluationMode
                    );

                // Looping through the dictionary
                foreach (var pair in sourceObjectValidated)
                {
                    if (pair.Value == false)
                    {
                        if (!resultList.ContainsKey(pair.Key)) // Prevent incorrect links to be added multiple times
                        {
                            resultList.Add(pair.Key, pair.Value); // Add objects that did not pass the test
                        }
                    }
                }
            }
            #endregion

            // Return the results back to the user
            if (resultList.Count > 0)
            {
                foreach (var sourceObjectResult in resultList)
                {
                    _alertValidation.SetTextLogging("     "+sourceObjectResult.Key + " is tested with this outcome: " + sourceObjectResult.Value + "\r\n");
                }

                MetadataParameters.ValidationIssues = MetadataParameters.ValidationIssues + resultList.Count();
                _alertValidation.SetTextLogging("\r\n");
            }
            else
            {
                _alertValidation.SetTextLogging("     There were no validation issues related to order of business keys in the Link tables.\r\n\r\n");
            }
        }

        /// <summary>
        /// Checks if all the supporting mappings are available (e.g. a Context table also needs a Core Business Concept present.
        /// </summary>
        internal void ValidateLogicalGroup()
        {
            string evaluationMode = radioButtonPhysicalMode.Checked ? "physical" : "virtual";

            #region Retrieving the Integration Layer tables
            // Informing the user.
            _alertValidation.SetTextLogging("--> Commencing the validation to check if the functional dependencies (logical group / unit of work) are present.\r\n");

            // Creating a list of tables which are dependent on other tables being present
            var objectList = new List<Tuple<string, string, string>>();
            foreach (DataGridViewRow row in dataGridViewTableMetadata.Rows)
            {
                if (!row.IsNewRow && (row.Cells[(int)TableMappingMetadataColumns.TargetTable].Value.ToString().StartsWith(TeamConfigurationSettings.LinkTablePrefixValue) || row.Cells[(int)TableMappingMetadataColumns.TargetTable].Value.ToString().StartsWith(TeamConfigurationSettings.SatTablePrefixValue) || row.Cells[(int)TableMappingMetadataColumns.TargetTable].Value.ToString().StartsWith(TeamConfigurationSettings.LsatTablePrefixValue))  )
                {
                    var businessKey = row.Cells[(int)TableMappingMetadataColumns.BusinessKeyDefinition].Value.ToString().Replace("''''", "'");
                    if (!objectList.Contains(new Tuple<string, string, string>(row.Cells[(int)TableMappingMetadataColumns.SourceTable].Value.ToString(), row.Cells[(int)TableMappingMetadataColumns.TargetTable].Value.ToString(), businessKey)))
                    {
                        objectList.Add(new Tuple<string, string, string>(row.Cells[(int)TableMappingMetadataColumns.SourceTable].Value.ToString(), row.Cells[(int)TableMappingMetadataColumns.TargetTable].Value.ToString(), businessKey));
                    }
                }
            }

            // Execute the validation check using the list of unique objects
            var resultList = new Dictionary<string, bool>();

            foreach (var sourceObject in objectList)
            {
                // The validation check returns a Dictionary
                var sourceObjectValidated = ClassMetadataValidation.ValidateLogicalGroup(sourceObject, TeamConfigurationSettings.MetadataConnection.CreateSqlServerConnectionString(false), GlobalParameters.CurrentVersionId, (DataTable)_bindingSourceTableMetadata.DataSource);

                // Looping through the dictionary
                foreach (var pair in sourceObjectValidated)
                {
                    if (pair.Value == false)
                    {
                        if (!resultList.ContainsKey(pair.Key)) // Prevent incorrect links to be added multiple times
                        {
                            resultList.Add(pair.Key, pair.Value); // Add objects that did not pass the test
                        }
                    }
                }
            }
            #endregion

            // Return the results back to the user
            if (resultList.Count > 0)
            {
                foreach (var sourceObjectResult in resultList)
                {
                    _alertValidation.SetTextLogging("     "+sourceObjectResult.Key + " is tested with this outcome: " + sourceObjectResult.Value + "\r\n");
                }

                _alertValidation.SetTextLogging("\r\n");
                MetadataParameters.ValidationIssues = MetadataParameters.ValidationIssues + resultList.Count();
            }
            else
            {
                _alertValidation.SetTextLogging("     There were no validation issues related to order of business keys in the Link tables.\r\n\r\n");
            }
        }

        /// <summary>
        ///   A validation check to make sure the Business Key is available in the source model.
        /// </summary>
        private void ValidateBusinessKeyObject()
        {
            string evaluationMode = radioButtonPhysicalMode.Checked ? "physical" : "virtual";

            // Informing the user.
            _alertValidation.SetTextLogging("--> Commencing the validation to determine if the Business Key metadata attributes exist in the physical model.\r\n");

            var localConnectionDictionary =
                LocalConnectionDictionary.GetLocalConnectionDictionary(TeamConfigurationSettings.ConnectionDictionary);


            var resultList = new Dictionary<Tuple<string, string>, bool>();
            foreach (DataGridViewRow row in dataGridViewTableMetadata.Rows)
            {
                if (!row.IsNewRow && (bool)row.Cells[TableMappingMetadataColumns.Enabled.ToString()].Value == true)
                {
                    Dictionary<Tuple<string, string>, bool> objectValidated = new Dictionary<Tuple<string, string>, bool>();
                    Tuple<string, string> validationObject = new Tuple<string, string>(row.Cells[(int)TableMappingMetadataColumns.SourceTable].Value.ToString(), row.Cells[(int)TableMappingMetadataColumns.BusinessKeyDefinition].Value.ToString());
                    
                    if (evaluationMode == "physical" && MetadataHandling.GetTableType(validationObject.Item1,"", TeamConfigurationSettings).ToString() != MetadataHandling.TableTypes.Source.ToString()) // No need to evaluate the operational system (real sources)
                    {

                        // Derive the connection
                        localConnectionDictionary.TryGetValue(row.Cells[(int)TableMappingMetadataColumns.SourceConnection].Value.ToString(), out var connectionValue);

                        try
                        {
                            objectValidated = ClassMetadataValidation.ValidateSourceBusinessKeyExistencePhysical(validationObject, connectionValue);
                        }
                        catch
                        {
                            _alertValidation.SetTextLogging("     An issue occurred connecting to the database while looking up physical model references.\r\n");
                        }
                    }
                    else if (evaluationMode == "virtual")
                    {
                        // Exclude a lookup to the source
                        if (MetadataHandling.GetTableType(validationObject.Item1,"", TeamConfigurationSettings).ToString() != MetadataHandling.TableTypes.Source.ToString())
                        { 
                            objectValidated = ClassMetadataValidation.ValidateSourceBusinessKeyExistenceVirtual(validationObject, (DataTable)_bindingSourcePhysicalModelMetadata.DataSource);
                        }
                    }
                    else
                    {
                        if (MetadataHandling.GetTableType(validationObject.Item1,"", TeamConfigurationSettings).ToString() !=
                            MetadataHandling.TableTypes.Source.ToString())
                        {
                            _alertValidation.SetTextLogging("     The validation approach (physical/virtual) could not be asserted.\r\n");
                        }
                    }

                    // Add negative results to dictionary
                    foreach (var objectValidationTuple in objectValidated)
                    {
                        if (objectValidationTuple.Value == false && !resultList.ContainsKey(objectValidationTuple.Key))
                        {
                            resultList.Add(objectValidationTuple.Key, false); // Add objects that did not pass the test
                        }
                    }
                }
            }

            // Return the results back to the user
            if (resultList.Count > 0)
            {
                foreach (var sourceObjectResult in resultList)
                {
                    _alertValidation.SetTextLogging("     Table " + sourceObjectResult.Key.Item1 + " does not contain Business Key attribute " + sourceObjectResult.Key.Item2 + ".\r\n");
                }

                MetadataParameters.ValidationIssues = MetadataParameters.ValidationIssues + resultList.Count();
            }
            else
            {
                _alertValidation.SetTextLogging("     There were no validation issues related to the existence of the business keys in the Source tables.\r\n");
            }

            _alertValidation.SetTextLogging("\r\n");
        }

        private void backgroundWorkerValidationOnly_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            // Show the progress in main form (GUI)
            labelResult.Text = (e.ProgressPercentage + "%");

            // Pass the progress to AlertForm label and progressbar
            _alertValidation.Message = "In progress, please wait... " + e.ProgressPercentage + "%";
            _alertValidation.ProgressValue = e.ProgressPercentage;
        }

        private void backgroundWorkerValidationOnly_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (e.Cancelled)
            {
                labelResult.Text = "Cancelled!";
            }
            else if (e.Error != null)
            {
                labelResult.Text = "Error: " + e.Error.Message;
            }
            else
            {
                labelResult.Text = "Done!";
                richTextBoxInformation.Text += "\r\nThe metadata was validated successfully!\r\n";
            }
        }

        private void FormManageMetadata_Shown(object sender, EventArgs e)
        {
            GridAutoLayout();
        }

        private void deleteThisRowFromTheGridToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            Int32 rowToDelete = dataGridViewAttributeMetadata.Rows.GetFirstRow(DataGridViewElementStates.Selected);
            dataGridViewAttributeMetadata.Rows.RemoveAt(rowToDelete);
        }

        private void deleteThisRowFromTheGridToolStripMenuItem2_Click(object sender, EventArgs e)
        {
            Int32 rowToDelete = dataGridViewPhysicalModelMetadata.Rows.GetFirstRow(DataGridViewElementStates.Selected);
            dataGridViewPhysicalModelMetadata.Rows.RemoveAt(rowToDelete);
        }

        private void displayTableScriptToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // Retrieve the index of the selected row
            Int32 selectedRow = dataGridViewPhysicalModelMetadata.Rows.GetFirstRow(DataGridViewElementStates.Selected);

            DataTable gridDataTable = (DataTable)_bindingSourcePhysicalModelMetadata.DataSource;
            DataTable dt2 = gridDataTable.Clone();
            dt2.Columns["ORDINAL_POSITION"].DataType = Type.GetType("System.Int32");

            foreach (DataRow dr in gridDataTable.Rows)
            {
                dt2.ImportRow(dr);
            }
            dt2.AcceptChanges();

            // Make sure the output is sorted
            dt2.DefaultView.Sort = "[TABLE_NAME] ASC, [ORDINAL_POSITION] ASC";

            // Retrieve all rows relative to the selected row (e.g. all attributes for the table)
            IEnumerable<DataRow> rows = dt2.DefaultView.ToTable().AsEnumerable().Where(r =>
                r.Field<string>("TABLE_NAME") ==
                dataGridViewPhysicalModelMetadata.Rows[selectedRow].Cells[4].Value.ToString()
                && r.Field<string>("SCHEMA_NAME") ==
                dataGridViewPhysicalModelMetadata.Rows[selectedRow].Cells[3].Value.ToString()
                && r.Field<string>("DATABASE_NAME") ==
                dataGridViewPhysicalModelMetadata.Rows[selectedRow].Cells[2].Value.ToString()
                );

            // Create a form and display the results
            var results = new StringBuilder();

            _generatedScripts = new Form_Alert();
            _generatedScripts.SetFormName("Display model metadata");
            _generatedScripts.Canceled += buttonCancel_Click;
            _generatedScripts.Show();

            results.AppendLine("IF OBJECT_ID('["+ dataGridViewPhysicalModelMetadata.Rows[selectedRow].Cells[4].Value + "]', 'U') IS NOT NULL");
            results.AppendLine("DROP TABLE [" + dataGridViewPhysicalModelMetadata.Rows[selectedRow].Cells[4].Value + "]");
            results.AppendLine();
            results.AppendLine("CREATE TABLE [" + dataGridViewPhysicalModelMetadata.Rows[selectedRow].Cells[4].Value + "]");
            results.AppendLine("(");

            int counter = 1;
            foreach (DataRow row in rows)
            {
                var commaSnippet = "";
                if (counter == 1)
                {
                    commaSnippet = "  ";
                }
                else
                {
                    commaSnippet = " ,";
                }

                counter++;
                results.AppendLine(commaSnippet + row["COLUMN_NAME"] + " -- with ordinal position of "+ row["ORDINAL_POSITION"]);
            }
            results.AppendLine(")");

            _generatedScripts.SetTextLogging(results.ToString());
            _generatedScripts.ProgressValue = 100;
            _generatedScripts.Message = "Done";
        }

        private void ButtonClickExportToJson(object sender, EventArgs e)
        {
            richTextBoxInformation.Clear();

            // Take all the rows from the grid
            List<DataGridViewRow> rowList = new List<DataGridViewRow>();
            foreach (DataGridViewRow row in dataGridViewTableMetadata.Rows)
            {
                if (!row.IsNewRow)
                {
                    rowList.Add(row); //add the row to the list
                }
            }

            GenerateFromPattern(rowList);
        }

        /// <summary>
        /// Creates a Json schema based on the Data Warehouse Automation interface definition.
        /// </summary>
        /// <param name="generationMetadataList"></param>
        private void GenerateFromPattern(List<DataGridViewRow> generationMetadataList)
        {
            // Set up the form in case the show Json output checkbox has been selected
            if (checkBoxShowJsonOutput.Checked)
            {
                _generatedJsonInterface = new Form_Alert();
                _generatedJsonInterface.SetFormName("Exporting the metadata as Json files");
                _generatedJsonInterface.ShowProgressBar(false);
                _generatedJsonInterface.ShowCancelButton(false);
                _generatedJsonInterface.ShowLogButton(false);
                _generatedJsonInterface.ShowProgressLabel(false);
                _generatedJsonInterface.Show();
            }

            int fileCounter = 0;

            EventLog eventLog = new EventLog();
            SqlConnection conn = new SqlConnection
            {
                ConnectionString = TeamConfigurationSettings.MetadataConnection.CreateSqlServerConnectionString(false)
            };

            foreach (DataGridViewRow metadataRow in generationMetadataList)
            {
                var sourceDataObjectName = metadataRow.Cells[(int) TableMappingMetadataColumns.SourceTable].Value.ToString();
                var targetDataObjectName = metadataRow.Cells[(int) TableMappingMetadataColumns.TargetTable].Value.ToString();
                var sourceConnectionInternalId = metadataRow.Cells[(int)TableMappingMetadataColumns.SourceConnection].Value.ToString();
                var targetConnectionInternalId = metadataRow.Cells[(int)TableMappingMetadataColumns.TargetConnection].Value.ToString();
                var filterCriterion = metadataRow.Cells[(int)TableMappingMetadataColumns.FilterCriterion].Value.ToString();
                var businessKeyDefinition = metadataRow.Cells[(int)TableMappingMetadataColumns.BusinessKeyDefinition].Value.ToString();
                var drivingKeyDefinition = metadataRow.Cells[(int)TableMappingMetadataColumns.DrivingKeyDefinition].Value.ToString();

                // Find out what the correct patterns is
                var tableType = MetadataHandling.GetTableType(targetDataObjectName, drivingKeyDefinition, TeamConfigurationSettings);
                LoadPatternDefinition loadPatternDefinition = GlobalParameters.PatternDefinitionList.First(item => item.LoadPatternType == tableType.ToString());

                var sourceConnection = new TeamConnection();
                TeamConfigurationSettings.ConnectionDictionary.TryGetValue(sourceConnectionInternalId, out sourceConnection);

                var targetConnection = new TeamConnection();
                TeamConfigurationSettings.ConnectionDictionary.TryGetValue(targetConnectionInternalId, out targetConnection);

                // Retrieve the source-to-target mappings (base query)
                DataTable metadataDataTable = new DataTable();
                try
                {
                    var metadataQuery = loadPatternDefinition.LoadPatternBaseQuery;
                    metadataDataTable = Utility.GetDataTable(ref conn, metadataQuery);
                }
                catch (Exception ex)
                {
                    eventLog.Add(Event.CreateNewEvent(EventTypes.Error, "The source-to-target mapping list could not be retrieved (baseQuery in PatternDefinition file). The error message is " + ex + ".\r\n"));
                }

                // Can contain multiple rows in metadata, because multiple sources can be mapped to a target.
                DataRow[] mappingRows = null;
                mappingRows = metadataDataTable.Select("[TARGET_NAME] = '" + targetDataObjectName + "'");

                // Populate the attribute mappings
                // Create the column-to-column mapping
                var columnMetadataQuery = loadPatternDefinition.LoadPatternAttributeQuery;
                var columnMetadataDataTable = Utility.GetDataTable(ref conn, columnMetadataQuery);

                // Populate the additional business key information (i.e. links)
                var additionalBusinessKeyQuery = loadPatternDefinition.LoadPatternAdditionalBusinessKeyQuery;
                var additionalBusinessKeyDataTable = Utility.GetDataTable(ref conn, additionalBusinessKeyQuery);

                // Select the right mapping and map the metadata to the DWH automation schema
                richTextBoxInformation.AppendText(@"Processing generation for " + targetDataObjectName + ".\r\n");

                // Move the data table to the class instance
                List<DataObjectMapping> sourceToTargetMappingList = new List<DataObjectMapping>();

                if (mappingRows != null)
                {
                    foreach (DataRow row in mappingRows)
                    {
                        #region Business Key

                        // Creating the Business Key definition, using the available components (see above)
                        List<BusinessKey> businessKeyList = new List<BusinessKey>();
                        BusinessKey businessKey =
                            new BusinessKey
                            {
                                businessKeyComponentMapping =
                                    InterfaceHandling.BusinessKeyComponentMappingList(
                                        (string) row["SOURCE_BUSINESS_KEY_DEFINITION"],
                                        (string) row["TARGET_BUSINESS_KEY_DEFINITION"]),
                                surrogateKey = (string) row["SURROGATE_KEY"]
                            };


                        // Create the classifications at Data Item (target) level, to capture if this attribute is a Multi-Active attribute.
                        if (row.Table.Columns.Contains("DRIVING_KEY_SOURCE"))
                        {
                            if (row["DRIVING_KEY_SOURCE"].ToString().Length > 0)
                            {
                                // Update the existing Business Key with a classification if a Driving Key exists.

                                foreach (var localDataItemMapping in businessKey.businessKeyComponentMapping)
                                {
                                    if (localDataItemMapping.sourceDataItem.name ==
                                        (string) row["DRIVING_KEY_SOURCE"])
                                    {

                                        List<Classification> dataItemClassificationList =
                                            new List<Classification>();
                                        var dataItemClassification = new Classification();
                                        dataItemClassification.classification = "DrivingKey";
                                        dataItemClassification.notes = "The attribute that triggers (drives) closing of a relationship.";
                                        dataItemClassificationList.Add(dataItemClassification);

                                        localDataItemMapping.sourceDataItem.dataItemClassification =
                                            dataItemClassificationList;
                                    }
                                }


                            }
                        }

                        businessKeyList.Add(businessKey);

                        #endregion

                        #region Data Item Mapping (column to column)

                        // Create the column-to-column mapping.
                        List<DataItemMapping> dataItemMappingList = new List<DataItemMapping>();
                        if (columnMetadataDataTable != null && columnMetadataDataTable.Rows.Count > 0)
                        {
                            DataRow[] columnRows = columnMetadataDataTable.Select(
                                "[TARGET_NAME] = '" + targetDataObjectName + "' AND [SOURCE_NAME] = '" +
                                (string) row["SOURCE_NAME"] + "'");

                            foreach (DataRow column in columnRows)
                            {
                                DataItemMapping columnMapping = new DataItemMapping();
                                DataItem sourceColumn = new DataItem();
                                DataItem targetColumn = new DataItem();

                                sourceColumn.name = (string) column["SOURCE_ATTRIBUTE_NAME"];
                                targetColumn.name = (string) column["TARGET_ATTRIBUTE_NAME"];

                                columnMapping.sourceDataItem = sourceColumn;
                                columnMapping.targetDataItem = targetColumn;

                                // Adding Multi-Active Key classification
                                if (column.Table.Columns.Contains("MULTI_ACTIVE_KEY_INDICATOR"))
                                {
                                    if ((string) column["MULTI_ACTIVE_KEY_INDICATOR"] == "Y")
                                    {
                                        // Create the classifications at Data Item (target) level, to capture if this attribute is a Multi-Active attribute.
                                        List<Classification> dataItemClassificationList =
                                            new List<Classification>();
                                        var dataItemClassification = new Classification();
                                        dataItemClassification.classification = "MultiActive";
                                        dataItemClassification.notes =
                                            "A multi-active attribute is part of the target table key.";
                                        dataItemClassificationList.Add(dataItemClassification);

                                        // Add the classification to the target Data Item
                                        columnMapping.targetDataItem.dataItemClassification =
                                            dataItemClassificationList;
                                    }
                                }

                                // Adding NULL classification
                                if ((string) column["SOURCE_ATTRIBUTE_NAME"] == "NULL")
                                {
                                    // Create the classifications at Data Item (target) level, to capture if this attribute is a NULL.
                                    List<Classification> dataItemClassificationList = new List<Classification>();
                                    var dataItemClassification = new Classification();
                                    dataItemClassification.classification = "NULL value";
                                    dataItemClassificationList.Add(dataItemClassification);

                                    // Add the classification to the target Data Item
                                    columnMapping.sourceDataItem.dataItemClassification = dataItemClassificationList;
                                }

                                dataItemMappingList.Add(columnMapping);
                            }
                        }

                        #endregion

                        #region Additional Business Keys

                        if (additionalBusinessKeyDataTable != null && additionalBusinessKeyDataTable.Rows.Count > 0)
                        {
                            DataRow[] additionalBusinessKeyRows =
                                additionalBusinessKeyDataTable.Select("[TARGET_NAME] = '" + targetDataObjectName + "'");

                            foreach (DataRow additionalKeyRow in additionalBusinessKeyRows)
                            {
                                var hubBusinessKey = new BusinessKey();

                                hubBusinessKey.businessKeyComponentMapping =
                                    InterfaceHandling.BusinessKeyComponentMappingList(
                                        (string) additionalKeyRow["SOURCE_BUSINESS_KEY_DEFINITION"],
                                        (string) additionalKeyRow["TARGET_BUSINESS_KEY_DEFINITION"]);
                                hubBusinessKey.surrogateKey = (string) additionalKeyRow["TARGET_KEY_NAME"];

                                if ((string) additionalKeyRow["HUB_NAME"] == "N/A")
                                {
                                    // Classification (degenerate field)
                                    List<Classification> businesskeyClassificationList = new List<Classification>();
                                    var businesskeyClassification = new Classification();
                                    businesskeyClassification.classification = "DegenerateAttribute";
                                    businesskeyClassification.notes =
                                        "Non Core Business Concept attribute, though part of the Relationship Key.";
                                    businesskeyClassificationList.Add(businesskeyClassification);

                                    hubBusinessKey.businessKeyClassification = businesskeyClassificationList;
                                }


                                businessKeyList.Add(hubBusinessKey); // Adding the Link Business Key
                            }
                        }

                        #endregion

                        #region Lookup Table

                        // Define a lookup table, in case there is a desire to do key lookups.
                        var lookupTable = (string) row["TARGET_NAME"];

                        if (TeamConfigurationSettings.TableNamingLocation == "Prefix")
                        {
                            int prefixLocation = lookupTable.IndexOf(TeamConfigurationSettings.StgTablePrefixValue);
                            if (prefixLocation != -1)
                            {
                                lookupTable = lookupTable
                                    .Remove(prefixLocation, TeamConfigurationSettings.StgTablePrefixValue.Length)
                                    .Insert(prefixLocation, TeamConfigurationSettings.PsaTablePrefixValue);
                            }
                        }
                        else
                        {
                            int prefixLocation = lookupTable.LastIndexOf(TeamConfigurationSettings.StgTablePrefixValue);
                            if (prefixLocation != -1)
                            {
                                lookupTable = lookupTable
                                    .Remove(prefixLocation, TeamConfigurationSettings.StgTablePrefixValue.Length)
                                    .Insert(prefixLocation, TeamConfigurationSettings.PsaTablePrefixValue);
                            }
                        }

                        #endregion

                        // Add the created Business Key to the source-to-target mapping.
                        var sourceToTargetMapping = new DataObjectMapping();

                        var sourceDataObject = new DataWarehouseAutomation.DataObject();
                        var targetDataObject = new DataWarehouseAutomation.DataObject();

                        sourceDataObject.name = (string) row["SOURCE_NAME"];
                        targetDataObject.name = (string) row["TARGET_NAME"];

                        // Source and target connection information
                        var sourceDataConnection = new DataConnection();
                        var targetDataConnection = new DataConnection();
                        
                        sourceDataConnection.dataConnectionString = sourceConnection.ConnectionKey;
                        targetDataConnection.dataConnectionString = targetConnection.ConnectionKey;

                        sourceDataObject.dataObjectConnection = sourceDataConnection;
                        targetDataObject.dataObjectConnection = targetDataConnection;

                        sourceToTargetMapping.sourceDataObject = sourceDataObject;
                        sourceToTargetMapping.targetDataObject = targetDataObject;
                        sourceToTargetMapping.enabled = true;

                        // Create a related data object to capture the lookup information.
                        // This needs to be put in a collection because the relatedDataObject is a List of Data Objects.
                        List<DataWarehouseAutomation.DataObject> relatedDataObject =
                            new List<DataWarehouseAutomation.DataObject>();
                        var lookupTableDataObject = new DataWarehouseAutomation.DataObject();
                        lookupTableDataObject.name = lookupTable;

                        // Create the classifications at Data Object level, to capture this is a Lookup relationship.
                        List<Classification> dataObjectClassificationList = new List<Classification>();
                        var dataObjectClassification = new Classification();
                        dataObjectClassification.classification = "Lookup";
                        dataObjectClassification.notes = "Lookup table related to the source-to-target mapping";
                        dataObjectClassificationList.Add(dataObjectClassification);

                        lookupTableDataObject.dataObjectClassification = dataObjectClassificationList;

                        relatedDataObject.Add(lookupTableDataObject);

                        sourceToTargetMapping.relatedDataObject = relatedDataObject;


                        //sourceToTargetMapping.lookupTable = lookupTable; // Lookup Table
                        sourceToTargetMapping.mappingName =
                            (string) row["TARGET_NAME"]; // Source-to-target mapping name
                        sourceToTargetMapping.businessKey = businessKeyList; // Business Key]

                        // Create the classifications at Data Object Mapping level.
                        List<Classification> dataObjectMappingClassificationList = new List<Classification>();
                        var dataObjectMappingClassification = new Classification();
                        dataObjectMappingClassification.id = loadPatternDefinition.LoadPatternKey;
                        dataObjectMappingClassification.classification = loadPatternDefinition.LoadPatternType;
                        dataObjectMappingClassification.notes = loadPatternDefinition.LoadPatternNotes;
                        dataObjectMappingClassificationList.Add(dataObjectMappingClassification);

                        sourceToTargetMapping.mappingClassification = dataObjectMappingClassificationList;
                        //sourceToTargetMapping.classification = MetadataHandling.GetTableType((string)row["TARGET_NAME"], "").Split(',').ToList();
                        //sourceToTargetMapping.classification = pattern.LoadPatternType.Split(',').ToList(); ;

                        sourceToTargetMapping.filterCriterion = (string) row["FILTER_CRITERIA"]; // Filter criterion

                        if (dataItemMappingList.Count == 0)
                        {
                            dataItemMappingList = null;
                        }

                        sourceToTargetMapping.dataItemMapping = dataItemMappingList; // Column to column mapping

                        // Add the source-to-target mapping to the mapping list
                        sourceToTargetMappingList.Add(sourceToTargetMapping);
                    }
                }

                // Create an instance of the non-generic information i.e. VEDW specific. For example the generation date/time.
                GenerationSpecificMetadata vedwMetadata = new GenerationSpecificMetadata();
                vedwMetadata.selectedDataObject = targetDataObjectName;

                // Create an instance of the 'MappingList' class / object model 
                VDW_DataObjectMappingList sourceTargetMappingList = new VDW_DataObjectMappingList();
                sourceTargetMappingList.dataObjectMappingList = sourceToTargetMappingList;

                sourceTargetMappingList.metadataConfiguration = new MetadataConfiguration();
                sourceTargetMappingList.metadataConfiguration.recordSourceAttribute =
                    TeamConfigurationSettings.RecordSourceAttribute;
                sourceTargetMappingList.metadataConfiguration.changeDataCaptureAttribute =
                    TeamConfigurationSettings.ChangeDataCaptureAttribute;
                sourceTargetMappingList.metadataConfiguration.etlProcessAttribute =
                    TeamConfigurationSettings.EtlProcessAttribute;
                sourceTargetMappingList.metadataConfiguration.eventDateTimeAttribute =
                    TeamConfigurationSettings.EventDateTimeAttribute;
                sourceTargetMappingList.metadataConfiguration.loadDateTimeAttribute =
                    TeamConfigurationSettings.LoadDateTimeAttribute;
                sourceTargetMappingList.metadataConfiguration.recordChecksumAttribute =
                    TeamConfigurationSettings.RecordChecksumAttribute;
                sourceTargetMappingList.metadataConfiguration.sourceRowIdAttribute =
                    TeamConfigurationSettings.RowIdAttribute;

                sourceTargetMappingList.generationSpecificMetadata = vedwMetadata;

                // Check if the metadata needs to be displayed
                try
                {
                    var json = JsonConvert.SerializeObject(sourceTargetMappingList, Formatting.Indented);

                    if (checkBoxShowJsonOutput.Checked)
                    {
                        _generatedJsonInterface.SetTextLogging(json + "\r\n\r\n");
                    }

                    // Spool the output to disk
                    if (checkBoxSaveInterfaceToJson.Checked)
                    {
                        Event fileSaveEventLog =
                            TeamUtility.SaveTextToFile(GlobalParameters.OutputPath + targetDataObjectName + ".json",
                                json);
                        eventLog.Add(fileSaveEventLog);
                        fileCounter++;
                    }
                }
                catch (Exception ex)
                {
                    richTextBoxInformation.AppendText(
                        "An error was encountered while generating the JSON metadata. The error message is: " + ex);
                }


                // END
            }

            // Report back to the user
            int errorCounter = 0;
            foreach (Event individualEvent in eventLog)
            {
                // Only report errors at this stage, can be extended with debug checkbox.
                if (individualEvent.eventCode == 1)
                {
                    errorCounter++;
                    richTextBoxInformation.AppendText(individualEvent.eventDescription);
                }
            }

            // Report back to the user
            richTextBoxInformation.AppendText($"\r\n{errorCounter} errors have been found.\r\n");
            richTextBoxInformation.AppendText($"\r\n{fileCounter} json schemas (files) have been prepared.\r\n");

            // Spool the output to disk
            if (checkBoxSaveInterfaceToJson.Checked)
            {
                richTextBoxInformation.AppendText(
                    $"Associated scripts have been saved in {GlobalParameters.OutputPath}.\r\n");
            }

            richTextBoxInformation.ScrollToCaret();

            conn.Close();
            conn.Dispose();
        }

        private void openConfigurationDirectoryToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                if (GlobalParameters.ConfigurationPath != "")
                {
                    Process.Start(GlobalParameters.ConfigurationPath);
                }
                else
                {
                    richTextBoxInformation.Text =
                        "There is no value given for the Configuration Path. Please enter a valid path name.";
                }
            }
            catch (Exception ex)
            {
                richTextBoxInformation.Text = "An error has occured while attempting to open the configuration directory. The error message is: " + ex;
            }
        }

        private void dataGridViewTableMetadata_EditingControlShowing(object sender, DataGridViewEditingControlShowingEventArgs e)
        {
            if (e.Control is DataGridViewComboBoxEditingControl tb)
            {
                tb.KeyDown -= DataGridViewTableMetadataKeyDown;
                tb.KeyDown += DataGridViewTableMetadataKeyDown;
            }
        }

        private void openAttributeMappingFileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var theDialog = new OpenFileDialog
            {
                Title = @"Open Attribute Mapping Metadata File",
                Filter = @"Attribute Mapping files|*.xml;*.json",
                InitialDirectory = GlobalParameters.ConfigurationPath //Application.StartupPath + @"\Configuration\"
            };

            var ret = STAShowDialog(theDialog);

            if (ret == DialogResult.OK)
            {
                try
                {
                    var chosenFile = theDialog.FileName;
                    var dataSet = new DataSet();

                    string fileExtension = Path.GetExtension(theDialog.FileName);

                    if (fileExtension == ".xml")
                    {
                        dataSet.ReadXml(chosenFile);

                        dataGridViewAttributeMetadata.DataSource = dataSet.Tables[0];
                        _bindingSourceAttributeMetadata.DataSource = dataGridViewAttributeMetadata.DataSource;
                    }
                    else if (fileExtension == ".json")
                    {
                        // Create a backup file, if enabled
                        if (checkBoxBackupFiles.Checked)
                        {
                            try
                            {
                                var backupFile = new JsonHandling();
                                var targetFileName = backupFile.BackupJsonFile(GlobalParameters.JsonAttributeMappingFileName + @"_v" + GlobalParameters.CurrentVersionId + ".json", GlobalParameters.ConfigurationPath);
                                richTextBoxInformation.Text = "A backup of the in-use JSON file was created as " + targetFileName + ".\r\n\r\n";
                            }
                            catch (Exception exception)
                            {
                                richTextBoxInformation.Text = "An issue occured when trying to make a backup of the in-use JSON file. The error message was " + exception + ".";
                            }
                        }

                        // If the information needs to be merged, a global parameter needs to be set.
                        // This will overwrite existing files for the in-use version.
                        if (!checkBoxMergeFiles.Checked)
                        {
                            JsonHandling.FileConfiguration.newFileAttributeMapping = "true";
                        }


                        // Load the file, convert it to a DataTable and bind it to the source
                        List<AttributeMappingJson> jsonArray = JsonConvert.DeserializeObject<List<AttributeMappingJson>>(File.ReadAllText(chosenFile));
                        DataTable dt = Utility.ConvertToDataTable(jsonArray);

                        // Set the column names in the datatable.
                        SetTeamDataTableProperties.SetAttributeDataTableColumns(dt);
                        // Sort the columns in the datatable.
                        SetTeamDataTableProperties.SetAttributeDatTableSorting(dt);

                        // Clear out the existing data from the grid
                        _bindingSourceAttributeMetadata.DataSource = null;
                        _bindingSourceAttributeMetadata.Clear();
                        dataGridViewAttributeMetadata.DataSource = null;

                        // Bind the datatable to the gridview
                        _bindingSourceAttributeMetadata.DataSource = dt;

                        if (jsonArray != null)
                        {
                            // Set the column header names.
                            dataGridViewAttributeMetadata.DataSource = _bindingSourceAttributeMetadata;
                            dataGridViewAttributeMetadata.ColumnHeadersVisible = true;
                            dataGridViewAttributeMetadata.Columns[0].Visible = false;
                            dataGridViewAttributeMetadata.Columns[1].Visible = false;
                            dataGridViewAttributeMetadata.Columns[6].ReadOnly = false;

                            dataGridViewAttributeMetadata.Columns[0].HeaderText = "Hash Key";
                            dataGridViewAttributeMetadata.Columns[1].HeaderText = "Version ID";
                            dataGridViewAttributeMetadata.Columns[2].HeaderText = "Source Table";
                            dataGridViewAttributeMetadata.Columns[3].HeaderText = "Source Column";
                            dataGridViewAttributeMetadata.Columns[4].HeaderText = "Target Table";
                            dataGridViewAttributeMetadata.Columns[5].HeaderText = "Target Column";
                            dataGridViewAttributeMetadata.Columns[6].HeaderText = "Notes";
                        }
                    }

                    GridAutoLayoutAttributeMetadata();
                    richTextBoxInformation.Text = "The metadata has been loaded from file.\r\n";
                    ContentCounter();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.ToString());
                }
            }
        }

        private void saveAttributeMappingAsJSONToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            try
            {
                var theDialog = new SaveFileDialog
                {
                    Title = @"Save Attribute Mapping Metadata File",
                    Filter = @"JSON files|*.json",
                    InitialDirectory = GlobalParameters.ConfigurationPath //Application.StartupPath + @"\Configuration\"
                };

                var ret = STAShowDialog(theDialog);

                if (ret == DialogResult.OK)
                {
                    try
                    {
                        var chosenFile = theDialog.FileName;

                        DataTable gridDataTable = (DataTable)_bindingSourceAttributeMetadata.DataSource;

                        // Make sure the output is sorted
                        gridDataTable.DefaultView.Sort = "[SOURCE_TABLE] ASC, [SOURCE_COLUMN] ASC, [TARGET_TABLE] ASC, [TARGET_COLUMN] ASC";

                        gridDataTable.TableName = "AttributeMappingMetadata";

                        JArray outputFileArray = new JArray();
                        foreach (DataRow singleRow in gridDataTable.DefaultView.ToTable().Rows)
                        {
                            JObject individualRow = JObject.FromObject(new
                            {
                                attributeMappingHash = singleRow[0].ToString(),
                                versionId = singleRow[1].ToString(),
                                sourceTable = singleRow[2].ToString(),
                                sourceAttribute = singleRow[3].ToString(),
                                targetTable = singleRow[4].ToString(),
                                targetAttribute = singleRow[5].ToString(),
                                Notes = singleRow[6].ToString()
                            });
                            outputFileArray.Add(individualRow);
                        }

                        string json = JsonConvert.SerializeObject(outputFileArray, Formatting.Indented);

                        File.WriteAllText(chosenFile, json);

                        richTextBoxInformation.Text = "The Attribute Mapping metadata file " + chosenFile + " saved successfully.";
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.ToString());
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("A problem occured when attempting to save the file to disk. The detail error message is: " + ex.Message);
            }
        }

        private void openPhysicalModelFileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var theDialog = new OpenFileDialog
            {
                Title = @"Open Physical Model Metadata File",
                Filter = @"Physical Model files|*.xml;*.json",
                InitialDirectory = GlobalParameters.ConfigurationPath
            };

            var ret = STAShowDialog(theDialog);

            if (ret == DialogResult.OK)
            {
                richTextBoxInformation.Clear();
                try
                {
                    var chosenFile = theDialog.FileName;
                    var dataSet = new DataSet();

                    string fileExtension = Path.GetExtension(theDialog.FileName);

                    if (fileExtension == ".xml")
                    {
                        dataSet.ReadXml(chosenFile);

                        dataGridViewPhysicalModelMetadata.DataSource = dataSet.Tables[0];
                        _bindingSourcePhysicalModelMetadata.DataSource = dataGridViewPhysicalModelMetadata.DataSource;

                    }
                    else if (fileExtension == ".json")
                    {
                        // Create a backup file, if enabled
                        if (checkBoxBackupFiles.Checked)
                        {
                            try
                            {
                                var backupFile = new JsonHandling();
                                var targetFileName = backupFile.BackupJsonFile(GlobalParameters.JsonModelMetadataFileName + @"_v" + GlobalParameters.CurrentVersionId + ".json", FormBase.GlobalParameters.ConfigurationPath);
                                richTextBoxInformation.Text = "A backup of the in-use JSON file was created as " + targetFileName + ".\r\n\r\n";
                            }
                            catch (Exception exception)
                            {
                                richTextBoxInformation.Text = "An issue occured when trying to make a backup of the in-use JSON file. The error message was " + exception + ".";
                            }
                        }

                        // If the information needs to be merged, a global parameter needs to be set.
                        // This will overwrite existing files for the in-use version.
                        if (!checkBoxMergeFiles.Checked)
                        {
                            JsonHandling.FileConfiguration.newFilePhysicalModel = "true";
                        }

                        // Load the file, convert it to a DataTable and bind it to the source
                        List<PhysicalModelMetadataJson> jsonArray = JsonConvert.DeserializeObject<List<PhysicalModelMetadataJson>>(File.ReadAllText(chosenFile));
                        DataTable dt = Utility.ConvertToDataTable(jsonArray);

                        // Setup the datatable with proper column headings.
                        SetTeamDataTableProperties.SetPhysicalModelDataTableColumns(dt);
                        // Sort the columns.
                        SetTeamDataTableProperties.SetPhysicalModelDataTableSorting(dt);

                        // Clear out the existing data from the grid
                        _bindingSourcePhysicalModelMetadata.DataSource = null;
                        _bindingSourcePhysicalModelMetadata.Clear();
                        dataGridViewPhysicalModelMetadata.DataSource = null;

                        // Bind the datatable to the gridview
                        _bindingSourcePhysicalModelMetadata.DataSource = dt;

                        if (jsonArray != null)
                        {
                            // Set the column header names
                            dataGridViewPhysicalModelMetadata.DataSource = _bindingSourcePhysicalModelMetadata;

                            dataGridViewPhysicalModelMetadata.ColumnHeadersVisible = true;
                            dataGridViewPhysicalModelMetadata.Columns[0].Visible = false;
                            dataGridViewPhysicalModelMetadata.Columns[1].Visible = false;

                            dataGridViewPhysicalModelMetadata.Columns[0].HeaderText = "Hash Key"; //Key column
                            dataGridViewPhysicalModelMetadata.Columns[1].HeaderText = "Version ID"; //Key column
                            dataGridViewPhysicalModelMetadata.Columns[2].HeaderText = "Database Name"; //Key column
                            dataGridViewPhysicalModelMetadata.Columns[3].HeaderText = "Schema Name"; //Key column
                            dataGridViewPhysicalModelMetadata.Columns[4].HeaderText = "Table Name"; //Key column
                            dataGridViewPhysicalModelMetadata.Columns[5].HeaderText = "Column Name"; //Key column
                            dataGridViewPhysicalModelMetadata.Columns[6].HeaderText = "Data Type";
                            dataGridViewPhysicalModelMetadata.Columns[7].HeaderText = "Length";
                            dataGridViewPhysicalModelMetadata.Columns[8].HeaderText = "Precision";
                            dataGridViewPhysicalModelMetadata.Columns[9].HeaderText = "Position";
                            dataGridViewPhysicalModelMetadata.Columns[10].HeaderText = "Primary Key";
                            dataGridViewPhysicalModelMetadata.Columns[11].HeaderText = "Multi-Active";
                        }
                    }

                    GridAutoLayoutPhysicalModelMetadata();
                    ContentCounter();
                    richTextBoxInformation.AppendText("The file " + chosenFile + " was loaded.\r\n");
                }
                catch (Exception ex)
                {
                    MessageBox.Show("An error has been encountered! The reported error is: " + ex);
                }
            }
        }

        private void exportPhysicalModelFileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                var theDialog = new SaveFileDialog
                {
                    Title = @"Save Model Metadata File",
                    Filter = @"JSON files|*.json",
                    InitialDirectory = GlobalParameters.ConfigurationPath //Application.StartupPath + @"\Configuration\"
                };

                var ret = STAShowDialog(theDialog);

                if (ret == DialogResult.OK)
                {
                    try
                    {
                        var chosenFile = theDialog.FileName;

                        DataTable gridDataTable = (DataTable)_bindingSourcePhysicalModelMetadata.DataSource;

                        gridDataTable.DefaultView.Sort = "[DATABASE_NAME], [SCHEMA_NAME], [TABLE_NAME] ASC, [ORDINAL_POSITION] ASC";

                        gridDataTable.TableName = "ModelMetadata";

                        JArray outputFileArray = new JArray();
                        foreach (DataRow singleRow in gridDataTable.DefaultView.ToTable().Rows)
                        {
                            JObject individualRow = JObject.FromObject(new
                            {
                                versionAttributeHash = singleRow[0].ToString(),
                                versionId = singleRow[1].ToString(),
                                databaseName = singleRow[2].ToString(),
                                schemaName = singleRow[3].ToString(),
                                tableName = singleRow[4].ToString(),
                                columnName = singleRow[5].ToString(),
                                dataType = singleRow[6].ToString(),
                                characterMaximumLength = singleRow[7].ToString(),
                                numericPrecision = singleRow[8].ToString(),
                                ordinalPosition = singleRow[9].ToString(),
                                primaryKeyIndicator = singleRow[10].ToString(),
                                multiActiveIndicator = singleRow[11].ToString()
                            });
                            outputFileArray.Add(individualRow);
                        }

                        string json = JsonConvert.SerializeObject(outputFileArray, Formatting.Indented);

                        File.WriteAllText(chosenFile, json);

                        richTextBoxInformation.Text = "The model metadata file " + chosenFile + " saved successfully.";
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.ToString());
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("A problem occured when attempting to save the file to disk. The detail error message is: " + ex.Message);
            }
        }
    }
}