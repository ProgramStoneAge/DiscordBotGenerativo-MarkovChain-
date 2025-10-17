using Microsoft.ML;
using Microsoft.ML.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

// SPDX-License-Identifier: GPL-3.0 
// Copyright (c) 2025 Victor Pereira

namespace MeuBotGenerativo
{
    public class ModelTrainer
    {
        // Classe para representar os dados de entrada
        public class ModelInput
        {
            [LoadColumn(0)]
            public string Intencao { get; set; }

            [LoadColumn(1)]
            public string Frase { get; set; }
        }

        // Classe para representar o resultado da previsão
        public class ModelOutput
        {
            [ColumnName("PredictedLabel")]
            public string PredictedIntencao { get; set; }
        }

        public static void Train()
        {
            var mlContext = new MLContext();

            // Carrega os dados do arquivo .tsv
            var dataView = mlContext.Data.LoadFromTextFile<ModelInput>("dados_ia_30.tsv", hasHeader: true);
            // Constrói o pipeline de treinamento
            var pipeline = mlContext.Transforms.Conversion.MapValueToKey(inputColumnName: "Intencao", outputColumnName: "Label")
                .Append(mlContext.Transforms.Text.FeaturizeText(inputColumnName: "Frase", outputColumnName: "FraseFeaturized"))
                .Append(mlContext.Transforms.Concatenate("Features", "FraseFeaturized"))
                .Append(mlContext.MulticlassClassification.Trainers.SdcaMaximumEntropy("Label", "Features"))
                .Append(mlContext.Transforms.Conversion.MapKeyToValue("PredictedLabel"));

            Console.WriteLine("Iniciando o treinamento do modelo de intenção...");

            // Treina o modelo
            var model = pipeline.Fit(dataView);

            Console.WriteLine("Treinamento concluído!");

            // Salva o modelo treinado em um arquivo .zip
            mlContext.Model.Save(model, dataView.Schema, "intent_model.zip");

            Console.WriteLine("Modelo salvo em intent_model.zip");
        }
    }
}
