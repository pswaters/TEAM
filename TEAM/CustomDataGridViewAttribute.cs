﻿using System.Drawing;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace TEAM
{
    class CustomDataGridViewAttribute : DataGridView
    {
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            ColourGridViewAttribute();
        }

        private void ColourGridViewAttribute()
        {
            var counter = 0;

            var hubIdentifier = "";
            var satIdentifier = "";
            var lnkIdentifier = "";
            var lsatIdentifier = "";
            var stgIdentifier = "";
            var psaIdentifier = "";
            var dimIdentifier = "";
            var factIdentifier = "";

            if (FormBase.TeamConfigurationSettings.TableNamingLocation == "Prefix")
            {
                hubIdentifier = FormBase.TeamConfigurationSettings.HubTablePrefixValue + "_";
                satIdentifier = FormBase.TeamConfigurationSettings.SatTablePrefixValue + "_";
                lnkIdentifier = FormBase.TeamConfigurationSettings.LinkTablePrefixValue + "_";
                lsatIdentifier = FormBase.TeamConfigurationSettings.LsatTablePrefixValue + "_";
                stgIdentifier = FormBase.TeamConfigurationSettings.StgTablePrefixValue + "_";
                psaIdentifier = FormBase.TeamConfigurationSettings.PsaTablePrefixValue + "_";
                dimIdentifier = "DIM_";
                factIdentifier = "FACT_";
            }
            else
            {
                hubIdentifier = '_' + FormBase.TeamConfigurationSettings.HubTablePrefixValue;
                satIdentifier = '_' + FormBase.TeamConfigurationSettings.SatTablePrefixValue;
                lnkIdentifier = '_' + FormBase.TeamConfigurationSettings.LinkTablePrefixValue;
                lsatIdentifier = '_' + FormBase.TeamConfigurationSettings.LsatTablePrefixValue;
                stgIdentifier = '_' + FormBase.TeamConfigurationSettings.StgTablePrefixValue;
                psaIdentifier = '_' + FormBase.TeamConfigurationSettings.PsaTablePrefixValue;
                dimIdentifier = "_DIM";
                factIdentifier = "_FACT";
            }

            foreach (DataGridViewRow row in Rows)
            {
               // var genericTable = row.Cells[0].Value;
                var integrationTable = row.Cells[4].Value;
                var businessKeySyntax = row.Cells[3].Value;

                if (integrationTable != null && businessKeySyntax != null && row.IsNewRow == false)
                {
                    // Backcolour for Integration Layer tables
                    if (Regex.Matches(integrationTable.ToString(), hubIdentifier).Count>0)
                    {
                        this[4, counter].Style.BackColor = Color.CornflowerBlue;
                    }
                    else if (Regex.Matches(integrationTable.ToString(), lsatIdentifier).Count > 0)
                    {
                        this[4, counter].Style.BackColor = Color.Gold;
                    }
                    else if (Regex.Matches(integrationTable.ToString(), satIdentifier).Count > 0)
                    {
                        this[4, counter].Style.BackColor = Color.Yellow;
                    }
                    else if (Regex.Matches(integrationTable.ToString(), lnkIdentifier).Count > 0)
                    {
                        this[4, counter].Style.BackColor = Color.OrangeRed;
                    }
                    else if (Regex.Matches(integrationTable.ToString(), psaIdentifier).Count > 0)
                    {
                        this[4, counter].Style.BackColor = Color.AntiqueWhite;
                    }
                    else if (Regex.Matches(integrationTable.ToString(), stgIdentifier).Count > 0)
                    {
                        this[4, counter].Style.BackColor = Color.WhiteSmoke;
                    }
                    else if (Regex.Matches(integrationTable.ToString(), dimIdentifier).Count > 0)
                    {
                        this[4, counter].Style.BackColor = Color.Aqua;
                    }
                    else if (Regex.Matches(integrationTable.ToString(), factIdentifier).Count > 0)
                    {
                        this[4, counter].Style.BackColor = Color.MediumAquamarine;
                    }

                    //Syntax highlighting for code
                    if (businessKeySyntax.ToString().Contains("COALESCE"))
                    {      
                        this[3, counter].Style.ForeColor = Color.DarkBlue;
                        this[3, counter].Style.Font = new Font("Microsoft Sans Serif", 8.25F, FontStyle.Bold);
                    }
                }

                counter++;
            }
        }
    }
}
