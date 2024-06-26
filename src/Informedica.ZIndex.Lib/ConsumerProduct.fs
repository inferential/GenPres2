namespace Informedica.ZIndex.Lib


module ConsumerProduct =

    open Informedica.Utils.Lib


    /// Creates a consumer product.
    let create id nm lb qt ct br =
        {
            Id = id
            Name = nm
            Label = lb
            Quantity = qt
            Container = ct
            BarCodes = br
        }


    let _get id =
        Zindex.BST004T.records ()
        |> Array.filter (fun r ->
            r.MUTKOD <> 1 &&
            r.HPKODE = id
        )
        |> Array.map (fun r ->
            let nm = Names.getName r.ATNMNR Names.Full
            let lb = Names.getName r.ATNMNR Names.Label
            let ct = Names.getThes r.VPDLOM Names.ConsumerContainer Names.Fifty

            let br =
                Zindex.BST200T.records ()
                |> Array.filter (fun b ->
                    b.ATKODE = r.ATKODE
                )
                |> Array.map (_.BARCOD)

            create r.ATKODE nm lb r.VPDLHV ct br
        )


    /// <summary>
    /// Gets the consumer products for the given id.
    /// </summary>
    /// <remarks>
    /// This is a memoized function.
    /// </remarks>
    let get : int -> ConsumerProduct [] = Memoization.memoize _get
