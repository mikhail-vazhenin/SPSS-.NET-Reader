﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using SpssLib.DataReader;
using SpssLib.FileParser.Records;
using SpssLib.SpssDataset;

namespace SpssLib.FileParser
{
	public class SavFileWriter : IDisposable
	{
		private readonly Stream _output;
		private readonly BinaryWriter _writer;
		private int _longNameCounter = 0;
		private Variable[] _variables;
		private IRecordWriter _recordWriter;
		private long _bias;
		private bool _compress;
	    private SpssOptions _options;
        
		public SavFileWriter(Stream output)
		{
			_output = output;
			// TODO set system constant with file format base encoding
			_writer = new BinaryWriter(_output, Encoding.ASCII);
		}

		public void WriteFileHeader(SpssOptions options, ICollection<Variable> variables)
		{
		    _options = options;
			_compress = options.Compressed;
			_bias = options.Bias;
			_variables = variables.ToArray();
			
			var headerRecords = new List<IRecord>();
            
			// SPSS file header
			headerRecords.Add(new HeaderRecord(options));

			// Process all variable info
			var variableLongNames = new Dictionary<string, string>();
			SetVaraibles(headerRecords, variableLongNames);

			// Integer & encoding info
			var intInfoRecord = new MachineIntegerInfoRecord(_options.HeaderEncoding);
			headerRecords.Add(intInfoRecord);

            // Integer & encoding info
            var fltInfoRecord = new MachineFloatingPointInfoRecord();
            headerRecords.Add(fltInfoRecord);
			
			// Variable Long names (as info record)
			if (variableLongNames.Any())
			{
				var longNameRecord = new LongVariableNamesRecord(variableLongNames, _options.HeaderEncoding);
				headerRecords.Add(longNameRecord);
			}

			// Char encoding info record (for data)
			var charEncodingRecord = new CharacterEncodingRecord(_options.DataEncoding);
			headerRecords.Add(charEncodingRecord);
			
			// End of the info records
			headerRecords.Add(new DictionaryTerminationRecord());


			// Write all of header, variable and info records
			foreach (var headerRecord in headerRecords)
			{
				headerRecord.WriteRecord(_writer);
			}
		}

		private void SetVaraibles(List<IRecord> headerRecords, IDictionary<string, string> variableLongNames)
		{
			var variableRecords = new List<VariableRecord>(_variables.Length);
            var valueLabels = new List<ValueLabel>(_variables.Length);

			// Read the variables and create the needed records
			ProcessVariables(variableLongNames, variableRecords, valueLabels);
			headerRecords.AddRange(variableRecords.Cast<IRecord>());
			
			// Set the count of varaibles as "nominal case size" on the HeaderRecord
			var header = headerRecords.OfType<HeaderRecord>().First();
			header.NominalCaseSize = variableRecords.Count;

			SetValueLabels(headerRecords, valueLabels);
		}

		private void SetValueLabels(List<IRecord> headerRecords, List<ValueLabel> valueLabels)
		{
			headerRecords.AddRange(valueLabels
									.Select(vl => new ValueLabelRecord(vl, _options.HeaderEncoding))
									.Cast<IRecord>());
		}

		private void ProcessVariables(IDictionary<string, string> variableLongNames, List<VariableRecord> variableRecords, List<ValueLabel> valueLabels)
		{
            var namesList = new SortedSet<byte[]>(new ByteArrayComparer());

            foreach (var variable in _variables)
			{
				int dictionaryIndex = variableRecords.Count + 1;

				var records = VariableRecord.GetNeededVaraibles(variable, _options.HeaderEncoding, namesList, ref _longNameCounter);
				variableRecords.AddRange(records);

				// Check if a longNameVariableRecord is needed
				if (records[0].Name != variable.Name)
				{
					variableLongNames.Add(records[0].Name, variable.Name);
				}

				// TODO Avoid repeating the same valueLabels on the file
				// Add ValueLabels if necesary
				if (variable.ValueLabels != null && variable.ValueLabels.Any())
				{
					var valueLabel = new ValueLabel(variable.ValueLabels.ToDictionary(p => BitConverter.GetBytes(p.Key), p => p.Value));
					valueLabel.VariableIndex.Add(dictionaryIndex);
					valueLabels.Add(valueLabel);
				}
			}
		}

	    private class ByteArrayComparer : IComparer<byte[]>
	    {
	        public int Compare(byte[] x, byte[] y)
	        {
	            var val = x[0] - y[0];
                for (int i = 0; val == 0 && ++i < x.Length; val = x[i] - y[i]);
	            return val;
	        }
	    }

	    public void Dispose()
		{
			_writer.Flush();
			_writer.Close();
			_output.Dispose();
		}

		public void WriteRecord(object[] record)
		{
			if (_recordWriter == null)
			{
				if (_compress)
				{
					_recordWriter = new CompressedRecordWriter(_writer, _bias, double.MinValue, _options.DataEncoding);
				}
				else
				{
					throw new NotImplementedException("Uncompressed data writing is not yet implemented. Please set compressed to true");
				}
			}

			for (int i = 0; i < _variables.Length; i++)
			{
				var variable = _variables[i];
				if (variable.Type == DataType.Numeric)
				{
					if (record[i] == null)
					{
						_recordWriter.WriteSysMiss();
					}
					else
					{
						_recordWriter.WriteNumber((double)record[i]);
					}
					
				}
				else
				{
					_recordWriter.WriteString((string) record[i], variable.TextWidth);
				}
			}
		}

		public void EndFile()
		{
			_recordWriter.EndFile();
		}
	}


	class ValueLabel
	{
        public IDictionary<byte[], string> Labels { get; private set; }

		public IList<int> VariableIndex
		{
			get { return _variableIndex; }
		}

		private IList<Int32> _variableIndex = new List<int>();

        public ValueLabel(IDictionary<byte[], string> labels)
		{
			Labels = labels;
		}
	}
}