﻿Option Strict On

' This program can be used to split apart a protein fasta file into a number of sections
' Although the splitting is random, each section will have a nearly identical number of residues
'
' -------------------------------------------------------------------------------
' Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA)
' Program started April 1, 2010

' E-mail: matthew.monroe@pnnl.gov or matt@alchemistmatt.com
' Website: http://omics.pnl.gov/ or http://www.sysbio.org/resources/staff/ or http://panomics.pnnl.gov/
' -------------------------------------------------------------------------------
' 
' Licensed under the Apache License, Version 2.0; you may not use this file except
' in compliance with the License.  You may obtain a copy of the License at 
' http://www.apache.org/licenses/LICENSE-2.0
'
' Notice: This computer software was prepared by Battelle Memorial Institute, 
' hereinafter the Contractor, under Contract No. DE-AC05-76RL0 1830 with the 
' Department of Energy (DOE).  All rights in the computer software are reserved 
' by DOE on behalf of the United States Government and the Contractor as 
' provided in the Contract.  NEITHER THE GOVERNMENT NOR THE CONTRACTOR MAKES ANY 
' WARRANTY, EXPRESS OR IMPLIED, OR ASSUMES ANY LIABILITY FOR THE USE OF THIS 
' SOFTWARE.  This notice including this sentence must appear on any copies of 
' this computer software.
Imports FastaFileSplitterDLL

Module modMain

	Public Const PROGRAM_DATE As String = "September 25, 2014"

    Private mInputFilePath As String

    Private mSplitCount As Integer

    Private mOutputFolderName As String             ' Optional
    Private mParameterFilePath As String            ' Optional

    Private mOutputFolderAlternatePath As String                ' Optional
    Private mRecreateFolderHierarchyInAlternatePath As Boolean  ' Optional

    Private mRecurseFolders As Boolean
    Private mRecurseFoldersMaxLevels As Integer

    Private mLogMessagesToFile As Boolean
    Private mQuietMode As Boolean

    Private WithEvents mFastaFileSplitter As clsFastaFileSplitter
    Private mLastProgressReportTime As System.DateTime
    Private mLastProgressReportValue As Integer

    Private Sub DisplayProgressPercent(ByVal intPercentComplete As Integer, ByVal blnAddCarriageReturn As Boolean)
        If blnAddCarriageReturn Then
            Console.WriteLine()
        End If
        If intPercentComplete > 100 Then intPercentComplete = 100
        Console.Write("Processing: " & intPercentComplete.ToString & "% ")
        If blnAddCarriageReturn Then
            Console.WriteLine()
        End If
    End Sub

    Public Function Main() As Integer
        ' Returns 0 if no error, error code if an error

        Dim intReturnCode As Integer
        Dim objParseCommandLine As New clsParseCommandLine
        Dim blnProceed As Boolean

        ' Initialize the options
        intReturnCode = 0
        mInputFilePath = String.Empty
        mSplitCount = clsFastaFileSplitter.DEFAULT_SPLIT_COUNT

        mOutputFolderName = String.Empty
        mParameterFilePath = String.Empty

        mRecurseFolders = False
        mRecurseFoldersMaxLevels = 0

        mQuietMode = False
        mLogMessagesToFile = False

		Try
			blnProceed = False
			If objParseCommandLine.ParseCommandLine Then
				If SetOptionsUsingCommandLineParameters(objParseCommandLine) Then blnProceed = True
			End If

			If Not blnProceed OrElse _
			   objParseCommandLine.NeedToShowHelp OrElse _
			   objParseCommandLine.ParameterCount + objParseCommandLine.NonSwitchParameterCount = 0 OrElse _
			   mInputFilePath.Length = 0 Then
				ShowProgramHelp()
				intReturnCode = -1
			Else

				mFastaFileSplitter = New clsFastaFileSplitter
				mFastaFileSplitter.ShowMessages = Not mQuietMode

				' Note: the following settings will be overridden if mParameterFilePath points to a valid parameter file that has these settings defined
				With mFastaFileSplitter
					.ShowMessages = Not mQuietMode
					.LogMessagesToFile = mLogMessagesToFile
					.SplitCount = mSplitCount
				End With

				If mRecurseFolders Then
					If mFastaFileSplitter.ProcessFilesAndRecurseFolders(mInputFilePath, mOutputFolderName, mOutputFolderAlternatePath, mRecreateFolderHierarchyInAlternatePath, mParameterFilePath, mRecurseFoldersMaxLevels) Then
						intReturnCode = 0
					Else
						intReturnCode = mFastaFileSplitter.ErrorCode
					End If
				Else
					If mFastaFileSplitter.ProcessFilesWildcard(mInputFilePath, mOutputFolderName, mParameterFilePath) Then
						intReturnCode = 0
					Else
						intReturnCode = mFastaFileSplitter.ErrorCode
						If intReturnCode <> 0 AndAlso Not mQuietMode Then
							Console.WriteLine("Error while processing: " & mFastaFileSplitter.GetErrorMessage())
						End If
					End If
				End If

				DisplayProgressPercent(mLastProgressReportValue, True)
			End If

		Catch ex As Exception
			ShowErrorMessage("Error occurred in modMain->Main: " & System.Environment.NewLine & ex.Message)
			intReturnCode = -1
		End Try

		Return intReturnCode

	End Function

    Private Function GetAppVersion() As String
        'Return System.Windows.Forms.Application.ProductVersion & " (" & PROGRAM_DATE & ")"

        Return System.Reflection.Assembly.GetExecutingAssembly.GetName.Version.ToString & " (" & PROGRAM_DATE & ")"
    End Function

    Private Function SetOptionsUsingCommandLineParameters(ByVal objParseCommandLine As clsParseCommandLine) As Boolean
        ' Returns True if no problems; otherwise, returns false

        Dim strValue As String = String.Empty
		Dim lstValidParameters As Generic.List(Of String) = New Generic.List(Of String) From {"I", "N", "O", "P", "S", "A", "R", "L", "Q"}

        Try
            ' Make sure no invalid parameters are present
			If objParseCommandLine.InvalidParametersPresent(lstValidParameters) Then
				ShowErrorMessage("Invalid commmand line parameters",
				  (From item In objParseCommandLine.InvalidParameters(lstValidParameters) Select "/" + item).ToList())
				Return False
			Else
				With objParseCommandLine
					' Query objParseCommandLine to see if various parameters are present
					If .RetrieveValueForParameter("I", strValue) Then
						mInputFilePath = strValue
					ElseIf .NonSwitchParameterCount > 0 Then
						mInputFilePath = .RetrieveNonSwitchParameter(0)
					End If

					If .RetrieveValueForParameter("N", strValue) Then
						If Not Integer.TryParse(strValue, mSplitCount) Then
							ShowErrorMessage("Error parsing number from the /N parameter; use /N:25 to specify the file be split into " & _
											 clsFastaFileSplitter.DEFAULT_SPLIT_COUNT & " parts")

							mSplitCount = clsFastaFileSplitter.DEFAULT_SPLIT_COUNT
						End If
					End If


					If .RetrieveValueForParameter("O", strValue) Then mOutputFolderName = strValue

					If .RetrieveValueForParameter("P", strValue) Then mParameterFilePath = strValue

					If .RetrieveValueForParameter("S", strValue) Then
						mRecurseFolders = True
						If Not Integer.TryParse(strValue, mRecurseFoldersMaxLevels) Then
							mRecurseFoldersMaxLevels = 0
						End If
					End If
					If .RetrieveValueForParameter("A", strValue) Then mOutputFolderAlternatePath = strValue
					If .RetrieveValueForParameter("R", strValue) Then mRecreateFolderHierarchyInAlternatePath = True

					If .RetrieveValueForParameter("L", strValue) Then mLogMessagesToFile = True
					If .RetrieveValueForParameter("Q", strValue) Then mQuietMode = True

				End With

				Return True
			End If

        Catch ex As Exception
             ShowErrorMessage("Error parsing the command line parameters: " & System.Environment.NewLine & ex.Message)
        End Try

		Return False

    End Function

    Private Sub ShowErrorMessage(ByVal strMessage As String)
        Dim strSeparator As String = "------------------------------------------------------------------------------"

        Console.WriteLine()
        Console.WriteLine(strSeparator)
        Console.WriteLine(strMessage)
        Console.WriteLine(strSeparator)
        Console.WriteLine()

    End Sub


	Private Sub ShowErrorMessage(ByVal strTitle As String, ByVal items As List(Of String))
		Dim strSeparator As String = "------------------------------------------------------------------------------"
		Dim strMessage As String

		Console.WriteLine()
		Console.WriteLine(strSeparator)
		Console.WriteLine(strTitle)
		strMessage = strTitle & ":"

		For Each item As String In items
			Console.WriteLine("   " + item)
			strMessage &= " " & item
		Next
		Console.WriteLine(strSeparator)
		Console.WriteLine()

		WriteToErrorStream(strMessage)
	End Sub

    Private Sub ShowProgramHelp()

        Try

            Console.WriteLine("This program can be used to split apart a protein fasta file into a number of sections. " & _
                              "Although the splitting is random, each section will have a nearly identical number of residues.")
            Console.WriteLine()

            Console.WriteLine("Program syntax:")
            Console.WriteLine(System.IO.Path.GetFileName(System.Reflection.Assembly.GetExecutingAssembly().Location) & _
                              " /I:SourceFastaFile [/O:OutputFolderPath]")
            Console.WriteLine(" [/N:SplitCount] [/P:ParameterFilePath] ")
            Console.WriteLine(" [/S:[MaxLevel]] [/A:AlternateOutputFolderPath] [/R] [/L] [/Q]")
            Console.WriteLine()
            Console.WriteLine("The input file path can contain the wildcard character * and should point to a fasta file. " & _
                              "The output folder switch is optional.  If omitted, the output file will be created in the same folder as the input file. ")
            Console.WriteLine()

            Console.WriteLine("Use /N to define the number of parts to split the input file into.")
            Console.WriteLine()

            Console.WriteLine("The parameter file path is optional.  If included, it should point to a valid XML parameter file.")
            Console.WriteLine()

            Console.WriteLine("Use /S to process all valid files in the input folder and subfolders. Include a number after /S (like /S:2) to limit the level of subfolders to examine. " & _
                              "When using /S, you can redirect the output of the results using /A. " & _
                              "When using /S, you can use /R to re-create the input folder hierarchy in the alternate output folder (if defined).")

            Console.WriteLine("Use /L to log messages to a file.  If /Q is used, then no messages will be displayed at the console.")
            Console.WriteLine()

            Console.WriteLine("Program written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA) in 2010")
            Console.WriteLine("Version: " & GetAppVersion())
            Console.WriteLine()

            Console.WriteLine("E-mail: matthew.monroe@pnnl.gov or matt@alchemistmatt.com")
            Console.WriteLine("Website: http://omics.pnl.gov/ or http://panomics.pnnl.gov/")
            Console.WriteLine()

            ' Delay for 750 msec in case the user double clicked this file from within Windows Explorer (or started the program via a shortcut)
            System.Threading.Thread.Sleep(750)

        Catch ex As Exception
            Console.WriteLine("Error displaying the program syntax: " & ex.Message)
        End Try

    End Sub

	Private Sub WriteToErrorStream(strErrorMessage As String)
		Try
			Using swErrorStream As System.IO.StreamWriter = New System.IO.StreamWriter(Console.OpenStandardError())
				swErrorStream.WriteLine(strErrorMessage)
			End Using
		Catch ex As Exception
			' Ignore errors here
		End Try
	End Sub

    Private Sub mFastaFileSplitter_ProgressChanged(ByVal taskDescription As String, ByVal percentComplete As Single) Handles mFastaFileSplitter.ProgressChanged
        Const PERCENT_REPORT_INTERVAL As Integer = 25
        Const PROGRESS_DOT_INTERVAL_MSEC As Integer = 250

        If percentComplete >= mLastProgressReportValue Then
            If mLastProgressReportValue > 0 Then
                Console.WriteLine()
            End If
            DisplayProgressPercent(mLastProgressReportValue, False)
            mLastProgressReportValue += PERCENT_REPORT_INTERVAL
            mLastProgressReportTime = DateTime.UtcNow
        Else
            If DateTime.UtcNow.Subtract(mLastProgressReportTime).TotalMilliseconds > PROGRESS_DOT_INTERVAL_MSEC Then
                mLastProgressReportTime = DateTime.UtcNow
                Console.Write(".")
            End If
        End If
    End Sub

    Private Sub mFastaFileSplitter_ProgressReset() Handles mFastaFileSplitter.ProgressReset
        mLastProgressReportTime = DateTime.UtcNow
        mLastProgressReportValue = 0
    End Sub
End Module
