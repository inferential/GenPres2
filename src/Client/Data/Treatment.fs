namespace Data

module Treatment =

    let joules =
        [ 
            1
            2
            3
            5
            7
            10
            20
            30
            50
            70
            100
            150
        ]

    let medicationDefs =
        [ 
            ( "reanimatie", "glucose 10%", 0.2, 0., 25., 0.1, "gram", "" )
            ( "reanimatie", "NaBic 8,4", 0.5, 0., 50., 1., "mmol", "" )
            ( "intubatie", "propofol 1%", 2., 0., 0., 10., "mg", "" )
            ( "intubatie", "propofol 2%", 2., 0., 0., 20., "mg", "" )
            ( "intubatie", "midazolam", 0.2, 0., 10., 5., "mg", "" )
            ( "intubatie", "esketamine", 0.5, 0., 5., 5., "mg", "" )
            ( "intubatie", "etomidaat", 0.5, 0., 20., 2., "mg", "" )
            ( "intubatie", "fentanyl", 1., 0., 50., 50., "mcg", "" )
            ( "intubatie", "morfine", 0.1, 0., 10., 1., "mg", "" )
            ( "intubatie", "rocuronium", 1., 0., 10., 10., "mg", "" )
            ( "intubatie", "atropine", 0.02, 0.1, 0.5, 0.5, "mg", "" )
            ( "antidota", "flumazine", 0.02, 0., 0.3, 0.1, "mg", "" )
            ( "antidota", "naloxon", 0.01, 0., 0.5, 0.02, "mg", "" )
            ( "elektrolyten", "kaliumchloride 7,4%", 0.5, 0., 40., 1.0, "mmol", "" )
            ( "elektrolyten", "calciumgluconaat", 0.13, 0., 4.5, 0.225, "mmol", "" )
            ( "elektrolyten", "magnesiumchloride 10%", 0.08, 0., 0., 0.5, "mmol", "" )
            ( "antiarrythmica", "adenosine 1e gift", 100., 0., 12000., 3000., "microg", "" )
            ( "antiarrythmica", "adenosine 2e gift", 200., 0., 12000., 3000., "microg", "" )
            ( "antiarrythmica", "adenosine 3e gift", 300., 0., 12000., 3000., "microg", "" )            
            ( "antiarrythmica", "amiodarone", 5., 0., 300., 50., "mg", "" )
            ( "anticonvulsiva", "diazepam", 0.5, 0., 10., 2., "mg", "" )
            ( "anticonvulsiva", "fenytoine", 20., 0., 1500., 50., "mg", "" )
            ( "anticonvulsiva", "midazolam", 0.1, 0., 10., 5., "mg", "" )
            ( "astma", "prednisolon", 2., 0., 25., 12.5, "mg", "" )
            ( "astma", "magnesiumsulfaat 16%", 40., 0., 2000., 160., "mg", "" )
            ( "astma", "magnesiumsulfaat 50%", 40., 0., 2000., 500., "mg", "" )
            ( "astma", "salbutamol oplaad", 15., 0., 0., 500., "microg", "" )
            ( "hersenoedeem", "mannitol 15%", 0.5, 0., 50., 0.15, "gram", "" )
            ( "lokaal anesthesie", "licocaine 1%", 5., 0., 200., 10., "mg", "" )
            ( "lokaal anesthesie", "licocaine 2%", 5., 0., 200., 20., "mg", "" )
        ]

    // Standaard oplossingen								Advies doseringen						
    // 2 tot 6		6 tot 11		11 tot 40		vanaf 40								
    // Tbl_Ped_MedContIV	Eenheid	Dos Eenheid	Hoev	Vol	Hoev	Vol	Hoev	Vol	Hoev	Vol	Min Dos	Max Dos	Abs Max	Min Conc	Max Conc	Opl Keuze	Adv

    let contMeds =
        [
            "adrenaline",  "mg",  "microg/kg/min",  1.,  50.,  2.,  50.,  5.,  50.,  5.,  50.,  0.05,  0.5,  1.,  0.,  0.1
            "albumine 20%",  "g",  "gram/kg/dag",  0.2,  1.,  0.2,  1.,  0.2,  1.,  0.2,  1.,  1.,  2.,  4.,  0.,  0.
            "alprostadil",  "mg",  "nanog/kg/min",  0.2,  50.,  0.,  0.,  0.,  0.,  0.,  0.,  10.,  50.,  100.,  0.,  0.02
            "amiodarone",  "mg",  "microg/kg/min",  50.,  50.,  150.,  50.,  300.,  50.,  600.,  50.,  5.,  15.,  25.,  0.6,  50.
            "clonidine",  "mg",  "microg/kg/uur",  0.15,  50.,  0.3,  50.,  0.6,  50.,  0.6,  50.,  0.25,  2.,  3.,  0.,  0.15
            "dobutamine",  "mg",  "microg/kg/min",  80.,  50.,  200.,  50.,  400.,  50.,  400.,  50.,  1.,  20.,  30.,  0.,  12.5
            "dopamine",  "mg",  "microg/kg/min",  80.,  50.,  200.,  50.,  400.,  50.,  400.,  50.,  1.,  20.,  30.,  0.,  40.
            "Epi bupi 1,25mg /ml",  "ml",  "ml/uur",  0.,  24.,  0.,  48.,  0.,  48.,  0.,  48.,  1.,  8.,  8.,  0.,  0.
            "Epi bupi 1,25mg, sufenta 0,5 mcg /ml",  "ml",  "ml/uur",  0.,  24.,  0.,  48.,  0.,  48.,  0.,  48.,  1.,  8.,  8.,  0.,  0.
            "epoprostenol",  "mg",  "nanog/kg/min",  0.2,  50.,  0.4,  50.,  0.8,  50.,  0.8,  50.,  0.5,  50.,  50.,  0.005,  0.01
            "esketamine",  "mg",  "mg/kg/uur",  100.,  50.,  250.,  50.,  250.,  50.,  250.,  50.,  0.5,  1.,  2.,  0.,  5.
            "esmolol",  "mg",  "mg/kg/min",  500.,  50.,  500.,  50.,  500.,  50.,  500.,  50.,  0.1,  1.,  2.,  0.,  10.
            "fentanyl",  "mg",  "microg/kg/uur",  0.5,  50.,  1.,  50.,  2.5,  50.,  2.5,  50.,  1.,  5.,  10.,  0.,  0.05
            "fenylefrine",  "mg",  "microg/kg/min",  1.5,  50.,  2.5,  50.,  5.,  50.,  5.,  50.,  0.05,  5.,  10.,  0.,  10.
            "furosemide",  "mg",  "mg/kg/dag",  10.,  50.,  20.,  50.,  40.,  50.,  100.,  50.,  1.,  4.,  6.,  0.,  10.
            "heparine",  "IE",  "IE/kg/uur",  5000.,  50.,  10000.,  50.,  20000.,  50.,  20000.,  50.,  10.,  20.,  50.,  0.,  1000.
            "insuline",  "IE",  "IE/kg/uur",  10.,  50.,  10.,  50.,  50.,  50.,  50.,  50.,  0.02,  0.125,  2.,  0.,  1.
            "isoprenaline",  "mg",  "microg/kg/min",  2.,  50.,  2.,  50.,  2.,  50.,  2.,  50.,  0.01,  1.5,  3.,  0.,  1.
            "labetalol",  "mg",  "mg/kg/uur",  250.,  50.,  250.,  50.,  250.,  50.,  250.,  50.,  0.25,  3.,  4.,  0.,  5.
            "magnesiumsulfaat",  "mg",  "mg/kg/uur",  500.,  50.,  1000.,  50.,  2000.,  50.,  2000.,  50.,  3.,  20.,  25.,  1.,  160.
            "midazolam",  "mg",  "mg/kg/uur",  25.,  50.,  50.,  50.,  50.,  50.,  100.,  50.,  0.05,  0.5,  1.,  0.,  5.
            "milrinone",  "mg",  "microg/kg/min",  5.,  50.,  10.,  50.,  20.,  50.,  20.,  50.,  0.15,  0.5,  0.75,  0.,  1.
            "morfine",  "mg",  "mg/kg/dag",  2.,  50.,  5.,  50.,  10.,  50.,  50.,  50.,  0.1,  0.5,  1.,  0.,  1.
            "NaCl 2,9%",  "mmol",  "mmol/kg/dag",  25.,  50.,  25.,  50.,  25.,  50.,  25.,  50.,  2.,  4.,  6.,  0.,  0.
            "nitroprusside",  "mg",  "microg/kg/min",  10.,  50.,  20.,  50.,  40.,  50.,  40.,  50.,  0.5,  8.,  10.,  0.,  10.
            "noradrenaline",  "mg",  "microg/kg/min",  1.,  50.,  2.,  50.,  5.,  50.,  5.,  50.,  0.05,  0.5,  1.,  0.,  1.
            "propofol 1%",  "mg",  "mg/kg/uur",  10.,  1.,  10.,  1.,  10.,  1.,  10.,  1.,  1.,  4.,  4.,  0.,  0.
            "propofol 2%",  "mg",  "mg/kg/uur",  20.,  1.,  20.,  1.,  20.,  1.,  20.,  1.,  1.,  4.,  4.,  0.,  0.
            "rocuronium",  "mg",  "mg/kg/uur",  50.,  50.,  100.,  50.,  200.,  50.,  200.,  50.,  0.6,  1.2,  2.,  0.,  10.
            "salbutamol",  "mg",  "microg/kg/min",  5.,  50.,  10.,  50.,  20.,  50.,  20.,  50.,  0.1,  10.,  15.,  0.005,  0.42
            "thiopental",  "mg",  "mg/kg/uur",  1250.,  50.,  1250.,  50.,  1250.,  50.,  1250.,  50.,  5.,  10.,  20.,  5.,  25.
        ]