﻿/*------------------------------------------------------------------------------------------------------------------------------

This application is intended as a proof of concept for demonstrating the RMS SDK capabilities.
It accepts a Microsoft Office or PDF document as input and encrypts it via either an Azure template or an ad hoc policy.

Code references AD RMS SDK 2.1 Interop Library
https://code.msdn.microsoft.com/windowsdesktop/AD-RMS-SDK-20-Interop-eb3fbce7
Code derived from Protecting Microsoft Azure Blob Storage with Microsoft Azure AD Rights Management in Cloud Services and Web Applications
https://msdnshared.blob.core.windows.net/media/MSDNBlogsFS/prod.evol.blogs.msdn.com/CommunityServer.Components.PostAttachments/00/10/52/89/53/Protecting%20Azure%20Blob%20Storage%20with%20Azure%20AD%20Rights%20Management.pdf

--------------------------------------------------------------------------------------------------------------------------------*/

using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using Microsoft.InformationProtectionAndControl;
using System.ComponentModel.DataAnnotations;
using System.Configuration;
using System.IO;
using System.Drawing;
using ZXing;

namespace AzureIP_test
{
    class Iprotect  
    {
        //Declare string constants to be used in application
        const string EncryptionMethod1 = "1";
        const string EncryptionMethod2 = "2";
        const string DecryptionMethod1 = "3";
        const string alreadyEncrypted = "encrypted";
    // if you are outside North America please uncomment this section as it is needed 
  /*   static Uri IntranetURL = new Uri(ConfigurationManager.AppSettings["LicensingIntranetDistributionPointUrl"]);
       static Uri ExtranetURL = new Uri(ConfigurationManager.AppSettings["LicensingExtranetDistributionPointUrl"]);
       static  ConnectionInfo connectionInfo = new ConnectionInfo(ExtranetURL, IntranetURL); */

        static void Main(string[] args)
        {
            //cria uma instância do leitor de código de barras
            var barcodeReader = new BarcodeReader();

            //carrega o bitmap do código a ser lido para a memória
            var barcodeBitmap = (Bitmap)Bitmap.FromFile(@"<CAMINHO DO ARQUIVO>sample.png");

            //decodifica o código de barras em memória
            var barcodeResult = barcodeReader.Decode(barcodeBitmap);

            //saída do resultado para o console
            Console.WriteLine("================================================================");
            Console.WriteLine(".NET Barcode reader + Azure Information Protection by Raposinha");
            Console.WriteLine("================================================================");
            Console.WriteLine("");
            Console.WriteLine("============================================================");
            Console.WriteLine("PASSO 1: Obter o conteúdo do código de barras e seu formato");
            Console.WriteLine("============================================================");
            Console.ReadLine();
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine(@"Caminho do arquivo a ser decodificado: <CAMINHO DO ARQUIVO>sample.png");
            Console.WriteLine($"Código de barras decodificado: {barcodeResult?.Text}");
            Console.WriteLine($"Formato do código de barras: {barcodeResult?.BarcodeFormat}");
            Console.ReadLine();
            Console.ForegroundColor = ConsoleColor.White;

            barcodeBitmap.Dispose();

            //Returns error if Main fails to execute correctly
            try
            {
                //Loads MSIPC.dll
                SafeNativeMethods.IpcInitialize();
                SafeNativeMethods.IpcSetAPIMode(APIMode.Server);
                //SafeNativeMethods.IpcSetStoreName("AzureIpTest");

                //Loads credentials for the service principal from App.Config 
                SymmetricKeyCredential symmetricKeyCred = new SymmetricKeyCredential();
                symmetricKeyCred.AppPrincipalId = ConfigurationManager.AppSettings["AppPrincipalId"];
                symmetricKeyCred.Base64Key = ConfigurationManager.AppSettings["Base64Key"];
                symmetricKeyCred.BposTenantId = ConfigurationManager.AppSettings["BposTenantId"];


                //Prompts user to choose whether to encrypt using Azure Template or Ad Hoc Policy
                Console.WriteLine("============================================================");
                Console.WriteLine("PASSO 2: Aplicar a política do Azure Information Protection");
                Console.WriteLine("============================================================");
                Console.WriteLine("");
                Console.WriteLine("Selecione o método de proteção desejado (Digite 1 ou 2):");
                Console.WriteLine("1. Proteger via Azure Template \n2. Proteger via Ad Hoc Policy");
                string method = Console.ReadLine();

                //Logic to handle user's encryption choice & invalid input
                if (method == EncryptionMethod1 || method == EncryptionMethod2)
                {
                    Console.WriteLine("");
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine(@"Caminho do arquivo a ser protegido: <CAMINHO DO ARQUIVO>sample.png");
                    Console.ForegroundColor = ConsoleColor.White;
                    string filePath = @"<CAMINHO DO ARQUIVO>sample.png";
                    Console.WriteLine("");
                    //Console.ReadLine();

                    //Returns error if no file path is entered
                    if (filePath.Trim() != "" && File.Exists(filePath))
                    {
                        //Checks the encryption status of file from the input path
                        var checkEncryptionStatus = SafeFileApiNativeMethods.IpcfIsFileEncrypted(filePath);
                        if (!checkEncryptionStatus.ToString().ToLower().Contains(alreadyEncrypted))
                        {
                            if (method == EncryptionMethod1)
                            {
                                //Encrypt a file via Azure Template
                                ProtectWithTemplate(symmetricKeyCred, filePath);

                            }
                            else if (method == EncryptionMethod2)
                            {
                                //Encrypt a file using Ad-Hoc policy
                                ProtectWithAdHocPolicy(symmetricKeyCred, filePath);
                            }
                        }
                        else
                        {
                            Console.WriteLine("The file has already been encrypted.");
                            Console.WriteLine("Would you like to decrypt it (Y/N) ? ");
                            string response = Console.ReadLine();
                            response = response.Trim().ToLower();
                            if (response == "y")
                            {
                                try
                                {
                                    string decryptedFilePath = SafeFileApiNativeMethods.IpcfDecryptFile(filePath.Trim(), SafeFileApiNativeMethods.DecryptFlags.IPCF_DF_FLAG_DEFAULT, false, false, false, IntPtr.Zero, null, null, null);
                                    Console.WriteLine(" The decrypted file is at the following location :" + decryptedFilePath);
                                } catch (Exception dx)
                                {
                                    Console.WriteLine("Error:" + dx);
                                }
                                

                            } else if (response.Trim().ToLower() =="n")
                            {
                                Console.WriteLine("Program Exiting .... ");
                                System.Environment.Exit(0);
                            } else
                            {
                                System.Environment.Exit(0);
                            }
                        }
                    } else
                    {
                        Console.WriteLine("Please enter a valid file path.");
                    }
                    }
                    else
                    {
                        Console.WriteLine("Invalid Input. Please enter 1, 2, or 3");
                    }
                } catch (Exception ex)
            {

                Console.WriteLine("An unexpected error occurred : {0}", ex);
            }

        }

        /// <summary> 
        /// Protect a file using an Azure Template 
        /// </summary>         
        /// <param name = "filePath" > input file path</param>
        /// <param name = " symmetricKeyCredential" > key storing the credentials for the service

        public static void ProtectWithTemplate(SymmetricKeyCredential symmetricKeyCredential, string filePath)
        {

            // If you are based outside of the North American geo you need to provide the connection info

           /* 
            Collection<TemplateInfo> templates = SafeNativeMethods.IpcGetTemplateList(connectionInfo, false, true,
                false, true, null, null, symmetricKeyCredential); */

            // Gets the available templates for this tenant  
            // if you uncomment the prior GetTemplateList call comment this call before you build           
            Collection<TemplateInfo> templates = SafeNativeMethods.IpcGetTemplateList(null, false, true, 
                false, true, null, null, symmetricKeyCredential);
                
            //Requests tenant template to use for encryption
            Console.WriteLine("Selecione o template desejado para proteger o arquivo:");
            Console.WriteLine("");

            //Outputs templates available for selection
            int counter = 0;
            for (int i = 0; i < templates.Count; i++)
            {
                counter++;
                Console.WriteLine(counter + ". " + templates.ElementAt(i).Name + "\n" +
                                  templates.ElementAt(i).Description);
            }

            //Parses template selection
            string input = Console.ReadLine();
            int templateSelection;
            bool parseResult = Int32.TryParse(input, out templateSelection);

            //Returns error if no template selection is entered
            if (parseResult)
            {
                //Ensures template value entered is valid
                if (0 < templateSelection && templateSelection <= counter)
                {
                    templateSelection = templateSelection - 1 ;

                    // Encrypts the file using the selected template             
                    TemplateInfo selectedTemplateInfo = templates.ElementAt(templateSelection);

                    string encryptedFilePath = SafeFileApiNativeMethods.IpcfEncryptFile(filePath,
                        selectedTemplateInfo.TemplateId,
                        SafeFileApiNativeMethods.EncryptFlags.IPCF_EF_FLAG_KEY_NO_PERSIST, true, false, true, null,
                        symmetricKeyCredential);
                }
                else
                {
                    Console.WriteLine("Please enter a valid template number.");
                }
            }
            else
            {
                Console.WriteLine("Please enter a valid template number.");
            }

        }

        /// <summary>         
        /// Protect a file using an ad-hoc policy       
        /// </summary>         
        /// <param name = "filePath" > input file path</param>
        /// <param name = " symmetricKeyCredential" > key storing the credentials for the service 

        public static void ProtectWithAdHocPolicy(SymmetricKeyCredential symmetricKeyCredential, string filePath)
        {
            //Requests policy owner
            Console.WriteLine("Please enter the policy owner's email.");
            string owner = Console.ReadLine();

            //Returns error if no owner email is entered
            if (owner.Trim() != "")
            {
                //Ensures that owner input is a valid email address
                if (isEmailValid(owner))
                {
                    //Requests users to whom rights will be given and add to list
                    Console.WriteLine(
                        "Please enter the email(s) of user(s) you would like to have rights to the file.\n" +
                        "Separate emails with spaces.");
                    string usersWithRights = Console.ReadLine();

                    //Returns error if no user email is entered
                    if (usersWithRights.Trim() != "")
                    {
                        bool userEmailsAreValid = true;
                        string[] usersWithRightsList = usersWithRights.Split(' ');

                        //Ensures that each user input is a valid email address
                        foreach (string email in usersWithRightsList)
                        {
                            if (!isEmailValid(email))
                            {
                                userEmailsAreValid = false;
                                Console.WriteLine("Please enter valid user email address(es).");
                                break;
                            }
                        }

                        if (userEmailsAreValid)
                        {
                            //Requests rights to give to specified users
                            Console.WriteLine("Please select the rights you would like user(s) to have.\n" +
                                              "Separate rights with spaces.");

                            //Outputs templates available for selection
                            CommonRights commonRights = new CommonRights();
                            foreach (var field in commonRights.GetType().GetFields())
                            {
                                Console.WriteLine("{0}", field.GetValue(commonRights));
                            }
                            string selectedRights = Console.ReadLine();

                            //Returns error if no right is entered
                            if (selectedRights.Trim() != "")
                            {
                                string[] selectedRightsList = selectedRights.Split(' ');
                                Collection<string> rightsCollection = new Collection<string>(selectedRightsList);

                                //Creates an ad hoc policy for specified users with specified rights
                                Collection<UserRights> userRights = new Collection<UserRights>();
                                foreach (string s in usersWithRightsList)
                                {
                                    userRights.Add(new UserRights(UserIdType.Email, s, rightsCollection));
                                }

                                Console.WriteLine("Please enter a name for this policy.");
                                string policyName = Console.ReadLine();

                                //Returns error if no policy name is entered
                                if (policyName.Trim() != "")
                                {
                                    Console.WriteLine("Please enter a description for this policy.");
                                    string policyDescription = Console.ReadLine();

                                    //Returns error if no policy description is entered
                                    if (policyDescription.Trim() != "")
                                    {
                                        Console.WriteLine("Please enter a display name for the policy issuer.");
                                        string issuerDisplayName = Console.ReadLine();

                                        //Returns error if no issuer display name is entered
                                        if (issuerDisplayName.Trim() != "")
                                        {
                                            // Gets the available issuers of rights policy templates.              
                                            // The available issuers is a list of RMS servers that this user has already contacted.
                                            try
                                            {

                                                // If you are based outside of the North American geo you need to provide the connection info

                                                /* 
                                                 Collection<TemplateIssuer> templateIssuers = SafeNativeMethods
                                                    .IpcGetTemplateIssuerList(
                                                        connectionInfo,
                                                        true,
                                                        false,
                                                        false, true, null, symmetricKeyCredential); */

                                                Collection<TemplateIssuer> templateIssuers = SafeNativeMethods
                                                    .IpcGetTemplateIssuerList(
                                                        null,
                                                        true,
                                                        false,
                                                        false, true, null, symmetricKeyCredential);

                                                // Creates the policy and associates the chosen user rights with it             
                                                SafeInformationProtectionLicenseHandle handle =
                                                    SafeNativeMethods.IpcCreateLicenseFromScratch(
                                                        templateIssuers.ElementAt(0));
                                                SafeNativeMethods.IpcSetLicenseOwner(handle, owner);
                                                SafeNativeMethods.IpcSetLicenseUserRightsList(handle, userRights);
                                                SafeNativeMethods.IpcSetLicenseDescriptor(handle,
                                                    new TemplateInfo(null, CultureInfo.CurrentCulture, policyName,
                                                        policyDescription, issuerDisplayName, false));

                                                //Encrypts the file using the ad hoc policy             
                                                string encryptedFilePath = SafeFileApiNativeMethods.IpcfEncryptFile(
                                                    filePath,
                                                    handle,
                                                    SafeFileApiNativeMethods.EncryptFlags.IPCF_EF_FLAG_KEY_NO_PERSIST,
                                                    true,
                                                    false,
                                                    true,
                                                    null,
                                                    symmetricKeyCredential);
                                            }
                                            catch (Exception)
                                            {
                                                Console.WriteLine(
                                                    "Please enter an owner and user(s) that exist in the Azure AD Tenant.");
                                            }
                                        }
                                        else
                                        {
                                            Console.WriteLine("Please enter a name for the policy issuer.");
                                        }
                                    }
                                    else
                                    {
                                        Console.WriteLine("Please enter a description for the policy.");
                                    }
                                }
                                else
                                {
                                    Console.WriteLine("Please enter a name for the policy.");
                                }
                            }
                            else
                            {
                                Console.WriteLine(
                                    "Please enter at least one right from the list. Multiple rights must be separated by spaces.");
                            }
                        }
                    }
                    else
                    {
                        Console.WriteLine("Please enter user email address(es). Multiple email addresses must be separated by spaces.");
                    }
                }
                else
                {
                    Console.WriteLine("Please enter a valid owner email.");
                }
            }
            else
            {
                Console.WriteLine("Please enter a valid owner email.");
            }
        }

        /// <summary>
        /// This helper function validates the format of the user-entered email address
        /// </summary>
        /// <param name="email"> input email adress</param>
        /// <returns>returns a bool stating whether the email address is valid</returns>
        public static bool isEmailValid(String email)
        {
            return new EmailAddressAttribute().IsValid(email);
        }
    } 
}
