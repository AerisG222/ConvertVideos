using System;
using System.Collections.Generic;
using System.IO;

namespace ConvertVideos.ResultWriter;

public class PgSqlResultWriter
    : IResultWriter
{
    public void WriteOutput(string outputFile, CategoryInfo category, IEnumerable<MovieMetadata> movies)
    {
        using var fs = new FileStream(outputFile, FileMode.CreateNew);
        using var writer = new StreamWriter(fs);

        writer.WriteLine($"INSERT INTO video.category (name, year) VALUES ({SqlString(category.Name)}, {category.Year});");

        foreach (var role in category.AllowedRoles)
        {
            writer.WriteLine(
                $"INSERT INTO video.category_role (category_id, role_id) VALUES (" +
                $"    (SELECT currval('video.category_id_seq')), " +
                $"    (SELECT id FROM maw.role WHERE name = {SqlString(role)})" +
                $" );"
            );
        }

        writer.WriteLine();

        bool hasSetTeaserVideo = false;

        foreach(var mm in movies)
        {
            writer.WriteLine(
                "INSERT INTO video.video (category_id, " +
                    $"thumb_height, thumb_width, thumb_path, thumb_size, " +
                    $"thumb_sq_height, thumb_sq_width, thumb_sq_path, thumb_sq_size, " +
                    $"full_height, full_width, full_path, full_size, " +
                    $"scaled_height, scaled_width, scaled_path, scaled_size, " +
                    $"raw_height, raw_width, raw_path, raw_size, " +
                    $"duration, create_date, " +
                    $"gps_latitude, gps_latitude_ref_id, gps_longitude, gps_longitude_ref_id) VALUES (" +
                    $"(SELECT currval('video.category_id_seq')), " +
                    $"{mm.ThumbHeight}, " +
                    $"{mm.ThumbWidth}, " +
                    $"{SqlString(mm.ThumbUrl)}, " +
                    $"{mm.ThumbSize}, " +
                    $"{mm.ThumbSqHeight}, " +
                    $"{mm.ThumbSqWidth}, " +
                    $"{SqlString(mm.ThumbSqUrl)}, " +
                    $"{mm.ThumbSqSize}, " +
                    $"{mm.FullHeight}, " +
                    $"{mm.FullWidth}, " +
                    $"{SqlString(mm.FullUrl)}, " +
                    $"{mm.FullSize}, " +
                    $"{mm.ScaledHeight}, " +
                    $"{mm.ScaledWidth}, " +
                    $"{SqlString(mm.ScaledUrl)}, " +
                    $"{mm.ScaledSize}, " +
                    $"{mm.RawHeight}, " +
                    $"{mm.RawWidth}, " +
                    $"{SqlString(mm.RawUrl)}, " +
                    $"{mm.RawSize}, " +
                    $"{SqlNumber(mm.VideoDuration)}, " +
                    $"{SqlTimestamp(mm.VideoCreationTime)}, " +
                    $"{SqlNumber(mm.Latitude)}, " +
                    $"{SqlString(mm.LatitudeRef)}, " +
                    $"{SqlNumber(mm.Longitude)}, " +
                    $"{SqlString(mm.LongitudeRef)} " +
                    $");");

            if (!hasSetTeaserVideo)
            {
                writer.WriteLine();
                writer.WriteLine(
                    $"UPDATE video.category " +
                    $"   SET teaser_image_path = {SqlString(mm.ThumbUrl)}, " +
                    $"       teaser_image_height = {mm.ThumbHeight}, " +
                    $"       teaser_image_width = {mm.ThumbWidth}, " +
                    $"       teaser_image_size = {mm.ThumbSize}, " +
                    $"       teaser_image_sq_path = {SqlString(mm.ThumbSqUrl)}, " +
                    $"       teaser_image_sq_height = {mm.ThumbSqHeight}, " +
                    $"       teaser_image_sq_width = {mm.ThumbSqWidth}, " +
                    $"       teaser_image_sq_size = {mm.ThumbSqSize} " +
                    $" WHERE id = (SELECT currval('video.category_id_seq'));");
                writer.WriteLine();

                hasSetTeaserVideo = true;
            }
        }

        writer.WriteLine();

        WriteCategoryUpdateTotals(writer);
    }

    void WriteCategoryUpdateTotals(StreamWriter writer)
    {
        writer.WriteLine(
            "UPDATE video.category c " +
            "   SET video_count = (SELECT COUNT(1) FROM video.video WHERE category_id = c.id), " +
            "       create_date = (SELECT create_date FROM video.video WHERE id = (SELECT MIN(id) FROM video.video where category_id = c.id AND create_date IS NOT NULL)), " +
            "       gps_latitude = (SELECT gps_latitude FROM video.video WHERE id = (SELECT MIN(id) FROM video.video WHERE category_id = c.id AND gps_latitude IS NOT NULL)), " +
            "       gps_latitude_ref_id = (SELECT gps_latitude_ref_id FROM video.video WHERE id = (SELECT MIN(id) FROM video.video WHERE category_id = c.id AND gps_latitude IS NOT NULL)), " +
            "       gps_longitude = (SELECT gps_longitude FROM video.video WHERE id = (SELECT MIN(id) FROM video.video WHERE category_id = c.id AND gps_latitude IS NOT NULL)), " +
            "       gps_longitude_ref_id = (SELECT gps_longitude_ref_id FROM video.video WHERE id = (SELECT MIN(id) FROM video.video WHERE category_id = c.id AND gps_latitude IS NOT NULL)), " +
            "       total_duration = (SELECT SUM(duration) FROM video.video WHERE category_id = c.id), " +
            "       total_size_thumb = (SELECT SUM(thumb_size) FROM video.video WHERE category_id = c.id), " +
            "       total_size_thumb_sq = (SELECT SUM(thumb_sq_size) FROM video.video WHERE category_id = c.id), " +
            "       total_size_scaled = (SELECT SUM(scaled_size) FROM video.video WHERE category_id = c.id), " +
            "       total_size_full = (SELECT SUM(full_size) FROM video.video WHERE category_id = c.id), " +
            "       total_size_raw = (SELECT SUM(raw_size) FROM video.video WHERE category_id = c.id) " +
            " WHERE id = (SELECT currval('video.category_id_seq'));"
        );
    }

    static string SqlNumber(object num)
    {
        if (num == null)
        {
            return "NULL";
        }

        return num.ToString();
    }

    static string SqlString(string val)
    {
        if (val == null)
        {
            return "NULL";
        }
        else
        {
            return $"'{val.Replace("'", "''")}'";
        }
    }

    string SqlTimestamp(DateTime? dt)
    {
        if (dt == null)
        {
            return "NULL";
        }

        return SqlString(((DateTime)dt).ToString("yyyy-MM-dd HH:mm:sszzz"));
    }
}
